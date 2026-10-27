using GithubAsanaSync.Functions.Models;
using Microsoft.Extensions.Logging;

namespace GithubAsanaSync.Functions.Services;

// The sync rules: which Asana task a GitHub issue/PR belongs to, and what
// each action (opened, edited, closed, reopened, merged) does to it. Kept
// out of the Functions layer so the rules can be tested without the
// Functions host and reused if the worker ever runs somewhere else.
public sealed class IssueSyncService(
    IAsanaClient asanaClient,
    IIdMappingStore idMappingStore,
    ILogger<IssueSyncService> logger) : IIssueSyncService
{
    public async Task HandleAsync(SyncMessage message, CancellationToken cancellationToken)
    {
        var taskGid = await idMappingStore.FindAsanaTaskGidAsync(message.Repository, message.SourceKind, message.Number, cancellationToken);

        if (taskGid is null)
        {
            var tag = message.SourceKind == GitHubSourceKind.PullRequest ? "PR" : "Issue";
            var title = $"[{tag}] {message.Title}";
            var notes = $"{message.Body}\n\n{message.HtmlUrl}";

            taskGid = await asanaClient.CreateTaskAsync(title, notes, cancellationToken);
            await idMappingStore.SaveMappingAsync(message.Repository, message.SourceKind, message.Number, taskGid, cancellationToken);

            logger.LogInformation(
                "Created Asana task {TaskGid} for {Repository} {SourceKind} #{Number}.",
                taskGid, message.Repository, message.SourceKind, message.Number);
        }

        switch (message.Action)
        {
            case GitHubIssueAction.Closed when message.SourceKind == GitHubSourceKind.PullRequest:
                await asanaClient.AddCommentAsync(
                    taskGid,
                    message.Merged ? $"Merged on GitHub: {message.HtmlUrl}" : $"Closed without merging: {message.HtmlUrl}",
                    cancellationToken);
                await asanaClient.UpdateTaskAsync(taskGid, completed: true, cancellationToken);
                break;

            case GitHubIssueAction.Closed:
                await SetCompletedIfDifferentAsync(taskGid, completed: true, cancellationToken);
                break;

            case GitHubIssueAction.Reopened:
                await SetCompletedIfDifferentAsync(taskGid, completed: false, cancellationToken);
                break;

            case GitHubIssueAction.Edited:
                await asanaClient.AddCommentAsync(taskGid, $"Edited on GitHub: {message.HtmlUrl}", cancellationToken);
                break;

            case GitHubIssueAction.Opened:
                // The CreateTaskAsync call above already covers this case.
                break;
        }
    }

    // Loop guard for the two-way sync: when the issue was closed or reopened
    // because of a change in Asana, the task is already in that state, so
    // writing it again would only trigger another Asana event.
    private async Task SetCompletedIfDifferentAsync(string taskGid, bool completed, CancellationToken cancellationToken)
    {
        if (await asanaClient.IsTaskCompletedAsync(taskGid, cancellationToken) == completed)
        {
            logger.LogInformation("Asana task {TaskGid} is already {State}; nothing to do.", taskGid, completed ? "completed" : "open");
            return;
        }

        await asanaClient.UpdateTaskAsync(taskGid, completed, cancellationToken);
    }
}
