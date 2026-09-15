namespace GithubAsanaSync.Functions.Services;

public interface IIdMappingStore
{
    // Returns the Asana task GID already linked to this GitHub issue, or
    // null if this issue has never been synced before.
    Task<string?> FindAsanaTaskGidAsync(string repository, int issueNumber, CancellationToken cancellationToken);

    Task SaveMappingAsync(string repository, int issueNumber, string asanaTaskGid, CancellationToken cancellationToken);
}
