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

    public async Task<string?> FindAsanaTaskGidAsync(string repository, GitHubSourceKind kind, int number, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<IssueMapping>(
                IssueMapping.BuildPartitionKey(repository),
                IssueMapping.BuildRowKey(kind, number),
                cancellationToken: cancellationToken);

            return response.Value.AsanaTaskGid;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task SaveMappingAsync(string repository, GitHubSourceKind kind, int number, string asanaTaskGid, CancellationToken cancellationToken)
    {
        var entity = new IssueMapping
        {
            PartitionKey = IssueMapping.BuildPartitionKey(repository),
            RowKey = IssueMapping.BuildRowKey(kind, number),
            AsanaTaskGid = asanaTaskGid,
        };

        await _tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);

        // Reverse row for the Asana -> GitHub direction. Its partition can
        // never collide with a repository partition, since BuildPartitionKey
        // always turns the '/' in "owner/name" into '_'.
        var reverse = new TableEntity(ReversePartitionKey, asanaTaskGid)
        {
            ["Repository"] = repository,
            ["SourceKind"] = kind.ToString(),
            ["Number"] = number,
        };

        await _tableClient.UpsertEntityAsync(reverse, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<GitHubItemRef?> FindGitHubItemAsync(string asanaTaskGid, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _tableClient.GetEntityAsync<TableEntity>(ReversePartitionKey, asanaTaskGid, cancellationToken: cancellationToken);
            var row = response.Value;

            return new GitHubItemRef(
                row.GetString("Repository"),
                Enum.Parse<GitHubSourceKind>(row.GetString("SourceKind")),
                row.GetInt32("Number") ?? throw new InvalidOperationException($"Mapping for task {asanaTaskGid} has no number."));
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    private const string ReversePartitionKey = "AsanaTask";
}
