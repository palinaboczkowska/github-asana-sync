using Azure;
using Azure.Data.Tables;
using GithubAsanaSync.Functions.Models;
using Microsoft.Extensions.Configuration;

namespace GithubAsanaSync.Functions.Services;

public sealed class TableIdMappingStore : IIdMappingStore
{
    private const string TableName = "IssueMappings";

    private readonly TableClient _tableClient;

    public TableIdMappingStore(IConfiguration configuration)
    {
        var connectionString = configuration["TableStorageConnection"]
            ?? throw new InvalidOperationException("TableStorageConnection is not configured.");

        _tableClient = new TableServiceClient(connectionString).GetTableClient(TableName);
        _tableClient.CreateIfNotExists();
    }

    public async Task<string?> FindAsanaTaskGidAsync(string repository, int issueNumber, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<IssueMapping>(
                repository,
                IssueMapping.BuildRowKey(issueNumber),
                cancellationToken: cancellationToken);

            return response.Value.AsanaTaskGid;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveMappingAsync(string repository, int issueNumber, string asanaTaskGid, CancellationToken cancellationToken)
    {
        var entity = new IssueMapping
        {
            PartitionKey = repository,
            RowKey = IssueMapping.BuildRowKey(issueNumber),
            AsanaTaskGid = asanaTaskGid,
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }
}
