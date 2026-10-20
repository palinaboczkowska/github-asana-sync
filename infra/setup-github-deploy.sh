#!/usr/bin/env bash
# One-time setup so the deploy job in .github/workflows/ci.yml can ship code
# to Azure without any stored Azure secret.
#
# Creates an Entra app that trusts GitHub's OIDC tokens for this repo's main
# branch only, gives it the two roles the deploy needs (upload the package
# blob, restart the Function App), and stores the non-secret IDs as GitHub
# repository variables. Safe to re-run: existing pieces are reused.
#
# Run from the repo root, logged in with `az login` and `gh auth login`.
set -euo pipefail

RG=rg-github-asana-sync
REPO=palinaboczkowska/github-asana-sync
APP_NAME=github-asana-sync-deploy

SUB=$(az account show --query id -o tsv)
TENANT=$(az account show --query tenantId -o tsv)
STORAGE_ID=$(az resource list -g "$RG" --resource-type Microsoft.Storage/storageAccounts --query "[0].id" -o tsv)
STORAGE=$(basename "$STORAGE_ID")
FUNC_ID=$(az resource list -g "$RG" --resource-type Microsoft.Web/sites --query "[0].id" -o tsv)
FUNC=$(basename "$FUNC_ID")

APP_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)
if [ -z "$APP_ID" ]; then
  APP_ID=$(az ad app create --display-name "$APP_NAME" --query appId -o tsv)
fi
SP_ID=$(az ad sp show --id "$APP_ID" --query id -o tsv 2>/dev/null || az ad sp create --id "$APP_ID" --query id -o tsv)

# GitHub presents its OIDC subject with the owner's and repo's immutable IDs
# (repo:owner@123/name@456:ref:...), so a renamed or re-registered account
# can never inherit the trust. Build the same string here.
OWNER_ID=$(gh api "repos/$REPO" --jq .owner.id)
REPO_ID=$(gh api "repos/$REPO" --jq .id)
SUBJECT="repo:${REPO%%/*}@$OWNER_ID/${REPO##*/}@$REPO_ID:ref:refs/heads/main"
CREDENTIAL="{
  \"name\": \"github-main\",
  \"issuer\": \"https://token.actions.githubusercontent.com\",
  \"subject\": \"$SUBJECT\",
  \"audiences\": [\"api://AzureADTokenExchange\"]
}"
if [ -z "$(az ad app federated-credential list --id "$APP_ID" --query "[?name=='github-main'].name" -o tsv)" ]; then
  az ad app federated-credential create --id "$APP_ID" --parameters "$CREDENTIAL" >/dev/null
else
  az ad app federated-credential update --id "$APP_ID" --federated-credential-id github-main --parameters "$CREDENTIAL" >/dev/null
fi

# Least privilege: write blobs in the one storage account, restart the one app.
az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role "Storage Blob Data Contributor" --scope "$STORAGE_ID" >/dev/null
az role assignment create --assignee-object-id "$SP_ID" --assignee-principal-type ServicePrincipal \
  --role "Website Contributor" --scope "$FUNC_ID" >/dev/null

gh variable set AZURE_CLIENT_ID --repo "$REPO" --body "$APP_ID"
gh variable set AZURE_TENANT_ID --repo "$REPO" --body "$TENANT"
gh variable set AZURE_SUBSCRIPTION_ID --repo "$REPO" --body "$SUB"
gh variable set AZURE_RESOURCE_GROUP --repo "$REPO" --body "$RG"
gh variable set AZURE_STORAGE_ACCOUNT --repo "$REPO" --body "$STORAGE"
gh variable set AZURE_FUNCTIONAPP --repo "$REPO" --body "$FUNC"

echo "Done. GitHub Actions can now deploy $FUNC from main."
