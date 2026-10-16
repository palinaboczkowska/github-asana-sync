// Infra for github-asana-sync: a Consumption-plan Function App wired to a
// Service Bus queue, a storage account (Functions runtime storage + the
// IssueMappings table), Key Vault for the two secrets the app needs, and
// Application Insights for tracing.
//
// Deploy with:
//   az deployment group create -g <resource-group> -f infra/main.bicep \
//     -p githubWebhookSecret=<secret> asanaAccessToken=<token> asanaProjectGid=<gid>

@description('Short, globally-unique-ish prefix used to derive resource names.')
param namePrefix string = 'ghasync'

@description('Azure region for every resource.')
param location string = resourceGroup().location

@secure()
param githubWebhookSecret string

@secure()
param asanaAccessToken string

param asanaProjectGid string

@description('Blob URL of the deployment package. The Function App reads it with its own managed identity, so no SAS token is needed.')
param packageUri string = ''

var storageAccountName = toLower('${namePrefix}st${uniqueString(resourceGroup().id)}')
var functionAppName = '${namePrefix}-func-${uniqueString(resourceGroup().id)}'
var serviceBusNamespaceName = '${namePrefix}-sb-${uniqueString(resourceGroup().id)}'
var keyVaultName = '${namePrefix}-kv-${uniqueString(resourceGroup().id)}'
var appInsightsName = '${namePrefix}-ai-${uniqueString(resourceGroup().id)}'
var logAnalyticsName = '${namePrefix}-log-${uniqueString(resourceGroup().id)}'
var hostingPlanName = '${namePrefix}-plan-${uniqueString(resourceGroup().id)}'
var queueName = 'github-issue-events'

// The Linux Consumption plan cannot fetch the deployment package with the
// app's own identity, so it needs a signed link. Azure mints it during the
// deployment, which keeps the token out of the parameters and off any
// command line.
var packageSasToken = empty(packageUri) ? '' : storageAccount.listServiceSas('2023-05-01', {
  canonicalizedResource: '/blob/${storageAccountName}/deployments/app.zip'
  signedResource: 'b'
  signedPermission: 'r'
  signedProtocol: 'https'
  signedExpiry: '2027-12-31T00:00:00Z'
}).serviceSasToken

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

// Basic tier keeps this on the cheapest possible Service Bus SKU — no free
// tier exists for Service Bus at any tier, so this is the floor, not a
// corner cut. Tear the resource group down between demos to avoid the
// per-day charge adding up.
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: serviceBusNamespaceName
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
}

resource serviceBusQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: queueName
  properties: {
    maxDeliveryCount: 10
    deadLetteringOnMessageExpiration: true
  }
}

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
  }
}

// Both secrets are written only when a value is passed in, so re-running the
// deployment without them leaves whatever is already in the vault alone.
resource githubWebhookSecretResource 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(githubWebhookSecret)) {
  parent: keyVault
  name: 'GitHubWebhookSecret'
  properties: { value: githubWebhookSecret }
}

resource asanaAccessTokenResource 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (!empty(asanaAccessToken)) {
  parent: keyVault
  name: 'AsanaAccessToken'
  properties: { value: asanaAccessToken }
}

// Y1 = Consumption plan: pay only for executions, scales to zero.
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: hostingPlanName
  location: location
  sku: { name: 'Y1', tier: 'Dynamic' }
  properties: {
    reserved: true
  }
}

resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNET-ISOLATED|8.0'
      appSettings: [
        { name: 'AzureWebJobsStorage', value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'WEBSITE_RUN_FROM_PACKAGE', value: empty(packageUri) ? '' : '${packageUri}?${packageSasToken}' }
        { name: 'FUNCTIONS_EXTENSION_VERSION', value: '~4' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ServiceBusConnection', value: listKeys('${serviceBusNamespace.id}/AuthorizationRules/RootManageSharedAccessKey', '2022-10-01-preview').primaryConnectionString }
        { name: 'TableStorageConnection', value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}' }
        { name: 'AsanaProjectGid', value: asanaProjectGid }
        { name: 'AsanaBaseUrl', value: 'https://app.asana.com/api/1.0' }
        // Resolved from Key Vault at runtime via the Function App's own
        // managed identity — the raw secret values never pass through this
        // template's app settings. The URIs carry no version, so rotating a
        // secret in the vault takes effect without a redeployment.
        { name: 'GitHubWebhookSecret', value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/GitHubWebhookSecret)' }
        { name: 'AsanaAccessToken', value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/AsanaAccessToken)' }
      ]
    }
  }
}

resource packageReaderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, functionAppName, 'Storage Blob Data Reader')
  scope: storageAccount
  properties: {
    // Storage Blob Data Reader
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource keyVaultSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, functionApp.id, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    // Key Vault Secrets User
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

output functionAppName string = functionApp.name
output functionAppHostname string = functionApp.properties.defaultHostName
output serviceBusNamespaceName string = serviceBusNamespace.name
output keyVaultName string = keyVault.name
