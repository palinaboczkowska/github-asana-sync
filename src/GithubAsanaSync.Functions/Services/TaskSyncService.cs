using GithubAsanaSync.Functions.Models;
using Microsoft.Extensions.Logging;

namespace GithubAsanaSync.Functions.Services;

public interface ITaskSyncService
{
    // Applies one Asana task change to the GitHub issue it belongs to.
    Task HandleAsync(AsanaTaskMessage message, CancellationToken cancellationToken);
}

// The Asana -> GitHub rules. Completing a task in Asana closes the issue,
// marking it incomplete reopens it.
//
// Loop guard: closing the issue makes GitHub send a "closed" webhook, which
// comes back to IssueSyncService and would complete the task again, which
// would make Asana send another event. Both directions therefore read the
// other side's current state and only write when it actually differs, so
// the echo finds nothing to do and stops after one round.
public sealed class TaskSyncService(
    IAsanaClient asanaClient,
    IGitHubClient gitHubClient,
    IIdMappingStore idMappingStore,
    ILogger<TaskSyncService> logger) : ITaskSyncService
{
    public async Task HandleAsync(AsanaTaskMessage message, CancellationToken cancellationToken)
    {
        var item = await idMappingStore.FindGitHubItemAsync(message.AsanaTaskGid, cancellationToken);

        if (item is null)
        {
            logger.LogInformation("Ignoring Asana task {TaskGid}: it was not created by this sync.", message.AsanaTaskGid);
            return;
        }

        // A pull request cannot be merged from Asana, and closing it there
        // would throw the work away, so pull requests stay one-way.
        if (item.Kind == GitHubSourceKind.PullRequest)
        {
            logger.LogInformation(
                "Ignoring Asana task {TaskGid}: it tracks {Repository} PR #{Number}, which only GitHub may close.",
                message.AsanaTaskGid, item.Repository, item.Number);
            return;
        }

        var taskCompleted = await asanaClient.IsTaskCompletedAsync(message.AsanaTaskGid, cancellationToken);
        var issueClosed = await gitHubClient.IsIssueClosedAsync(item.Repository, item.Number, cancellationToken);

        if (taskCompleted == issueClosed)
        {
            logger.LogInformation(
                "{Repository} #{Number} already matches Asana task {TaskGid}; nothing to do.",
                item.Repository, item.Number, message.AsanaTaskGid);
            return;
        }

        await gitHubClient.AddIssueCommentAsync(
            item.Repository,
            item.Number,
            taskCompleted ? "Completed in Asana, closing." : "Marked incomplete in Asana, reopening.",
            cancellationToken);
        await gitHubClient.SetIssueClosedAsync(item.Repository, item.Number, taskCompleted, cancellationToken);

        logger.LogInformation(
            "{Action} {Repository} #{Number} from Asana task {TaskGid}.",
            taskCompleted ? "Closed" : "Reopened", item.Repository, item.Number, message.AsanaTaskGid);
    }
}
