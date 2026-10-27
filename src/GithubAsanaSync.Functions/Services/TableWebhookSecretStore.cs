using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace GithubAsanaSync.Functions.Services;

public interface IWebhookSecretStore
{
    Task<string?> GetAsanaSecretAsync(CancellationToken cancellationToken);

    Task SaveAsanaSecretAsync(string secret, CancellationToken cancellationToken);
}

// Asana does not let you choose the webhook secret: it sends one during the
// handshake and signs every later delivery with it. The app has to keep it
// somewhere it can write to at runtime, and its Key Vault role is read-only,
// so it goes in Table Storage next to the id mappings.
public sealed class TableWebhookSecretStore : IWebhookSecretStore
{
    private const string TableName = "WebhookSecrets";
    private const string PartitionKey = "Asana";
    private const string RowKey = "Secret";

    private readonly TableClient _tableClient;

    public TableWebhookSecretStore(IConfiguration configuration)
    {
        var connectionString = configuration["TableStorageConnection"]
            ?? throw new InvalidOperationException("TableStorageConnection is not configured.");

        _tableClient = new TableServiceClient(connectionString).GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<string?> GetAsanaSecretAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(PartitionKey, RowKey, cancellationToken: cancellationToken);
            return response.Value.GetString("Value");
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveAsanaSecretAsync(string secret, CancellationToken cancellationToken)
    {
        var entity = new TableEntity(PartitionKey, RowKey) { ["Value"] = secret };
        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }
}
