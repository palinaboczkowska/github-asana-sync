using GithubAsanaSync.Functions.Models;

namespace GithubAsanaSync.Functions.Services;

public interface IIssueSyncService
{
    // Applies one GitHub issue/PR change to Asana.
    Task HandleAsync(SyncMessage message, CancellationToken cancellationToken);
}
