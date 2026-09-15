using GithubAsanaSync.Functions.Models;

namespace GithubAsanaSync.Functions.Services;

public interface IIdMappingStore
{
    // Returns the Asana task GID already linked to this GitHub issue or
    // pull request, or null if it has never been synced before.
    Task<string?> FindAsanaTaskGidAsync(string repository, GitHubSourceKind kind, int number, CancellationToken cancellationToken);

    Task SaveMappingAsync(string repository, GitHubSourceKind kind, int number, string asanaTaskGid, CancellationToken cancellationToken);
}
