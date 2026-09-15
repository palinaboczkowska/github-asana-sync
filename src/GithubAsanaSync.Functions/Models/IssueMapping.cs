using Azure;
using Azure.Data.Tables;

namespace GithubAsanaSync.Functions.Models;

// One row per synced GitHub issue. PartitionKey groups by repository so a
// lookup for "does this issue already have an Asana task" is a single
// point read instead of a table scan.
public sealed class IssueMapping : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // repository full name
    public string RowKey { get; set; } = string.Empty;       // issue number, as string
    public string AsanaTaskGid { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public static string BuildRowKey(int issueNumber) => issueNumber.ToString();
}
