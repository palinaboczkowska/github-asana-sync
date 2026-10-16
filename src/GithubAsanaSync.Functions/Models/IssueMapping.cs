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

    // Azure Table keys cannot contain '/', '\\', '#' or '?', and a GitHub
    // repository is always "owner/name". Storing the raw name makes every
    // request fail with "Error in query syntax", which only shows up against
    // real Table Storage — a fake store happily accepts any string.
    public static string BuildPartitionKey(string repository)
    {
        var key = new char[repository.Length];

        for (var i = 0; i < repository.Length; i++)
        {
            var c = repository[i];
            key[i] = c is '/' or '\\' or '#' or '?' || char.IsControl(c) ? '_' : c;
        }

        return new string(key);
    }
}
