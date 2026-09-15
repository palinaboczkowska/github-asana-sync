using Azure;
using Azure.Data.Tables;

namespace GithubAsanaSync.Functions.Models;

// One row per synced GitHub issue or pull request. PartitionKey groups by
// repository so a lookup for "does this item already have an Asana task"
// is a single point read instead of a table scan. RowKey is tagged with
// the source kind because issues and pull requests share one numbering
// sequence in GitHub — issue #42 and PR #42 can both exist in the same repo.
public sealed class IssueMapping : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // repository full name
    public string RowKey { get; set; } = string.Empty;       // "{kind}-{number}"
    public string AsanaTaskGid { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public static string BuildRowKey(GitHubSourceKind kind, int number) => $"{kind}-{number}";
}
