using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GithubAsanaSync.Tests;

public class TaskSyncServiceTests
{
    private readonly Mock<IAsanaClient> _asanaClient = new();
    private readonly Mock<IGitHubClient> _gitHubClient = new();
    private readonly Mock<IIdMappingStore> _idMappingStore = new();
    private readonly TaskSyncService _sut;

    public TaskSyncServiceTests()
    {
        _sut = new TaskSyncService(
            _asanaClient.Object,
            _gitHubClient.Object,
            _idMappingStore.Object,
            NullLogger<TaskSyncService>.Instance);
    }

    [Fact]
    public async Task Handle_TaskCompletedAndIssueOpen_ClosesIssueWithAComment()
    {
        MapTaskTo(GitHubSourceKind.Issue);
        TaskIsCompleted(true);
        IssueIsClosed(false);

        await Run();

        _gitHubClient.Verify(c => c.AddIssueCommentAsync("acme/widgets", 42, It.Is<string>(t => t.Contains("Completed in Asana")), It.IsAny<CancellationToken>()), Times.Once);
        _gitHubClient.Verify(c => c.SetIssueClosedAsync("acme/widgets", 42, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TaskReopenedAndIssueClosed_ReopensIssue()
    {
        MapTaskTo(GitHubSourceKind.Issue);
        TaskIsCompleted(false);
        IssueIsClosed(true);

        await Run();

        _gitHubClient.Verify(c => c.SetIssueClosedAsync("acme/widgets", 42, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_IssueAlreadyMatchesTask_WritesNothing(bool state)
    {
        // The echo of our own change: closing the issue made GitHub call the
        // other direction, which left the task as it was, and Asana's event
        // for it lands here with both sides already agreeing. Writing again
        // would keep the loop going.
        MapTaskTo(GitHubSourceKind.Issue);
        TaskIsCompleted(state);
        IssueIsClosed(state);

        await Run();

        _gitHubClient.Verify(c => c.SetIssueClosedAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        _gitHubClient.Verify(c => c.AddIssueCommentAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_TaskNotCreatedBySync_IsIgnored()
    {
        _idMappingStore
            .Setup(s => s.FindGitHubItemAsync("task-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync((GitHubItemRef?)null);

        await Run();

        _asanaClient.Verify(c => c.IsTaskCompletedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _gitHubClient.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_TaskTracksAPullRequest_NeverClosesItFromAsana()
    {
        MapTaskTo(GitHubSourceKind.PullRequest);
        TaskIsCompleted(true);

        await Run();

        _gitHubClient.VerifyNoOtherCalls();
    }

    private void MapTaskTo(GitHubSourceKind kind) =>
        _idMappingStore
            .Setup(s => s.FindGitHubItemAsync("task-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GitHubItemRef("acme/widgets", kind, 42));

    private void TaskIsCompleted(bool completed) =>
        _asanaClient
            .Setup(c => c.IsTaskCompletedAsync("task-123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(completed);

    private void IssueIsClosed(bool closed) =>
        _gitHubClient
            .Setup(c => c.IsIssueClosedAsync("acme/widgets", 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync(closed);

    private Task Run() =>
        _sut.HandleAsync(new AsanaTaskMessage("task-123"), CancellationToken.None);
}
