using GithubAsanaSync.Functions.Models;

namespace GithubAsanaSync.Functions.Services;

public interface IIdMappingStore
{
    // Returns the Asana task GID already linked to this GitHub issue or
    // pull request, or null if it has never been synced before.
    Task<string?> FindAsanaTaskGidAsync(string repository, GitHubSourceKind kind, int number, CancellationToken cancellationToken);

    // Saves the link in both directions, so an Asana event can find its way
    // back to the GitHub item.
    Task SaveMappingAsync(string repository, GitHubSourceKind kind, int number, string asanaTaskGid, CancellationToken cancellationToken);

    // Returns the GitHub item an Asana task was created for, or null for a
    // task this sync did not create (or one created before the reverse
    // mapping existed).
    Task<GitHubItemRef?> FindGitHubItemAsync(string asanaTaskGid, CancellationToken cancellationToken);
}
