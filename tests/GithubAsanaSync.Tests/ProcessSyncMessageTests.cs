using GithubAsanaSync.Functions.Functions;
using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace GithubAsanaSync.Tests;

public class ProcessSyncMessageTests
{
    private readonly Mock<IAsanaClient> _asanaClient = new();
    private readonly Mock<IIdMappingStore> _idMappingStore = new();
    private readonly ProcessSyncMessage _sut;

    public ProcessSyncMessageTests()
    {
        _sut = new ProcessSyncMessage(
            _asanaClient.Object,
            _idMappingStore.Object,
            NullLogger<ProcessSyncMessage>.Instance);
    }

    [Fact]
    public async Task Run_NewIssueOpened_CreatesTaskAndSavesMapping()
    {
        _idMappingStore
            .Setup(s => s.FindAsanaTaskGidAsync("acme/widgets", GitHubSourceKind.Issue, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _asanaClient
            .Setup(c => c.CreateTaskAsync("[Issue] Widget is broken", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-123");

        var message = new SyncMessage("acme/widgets", GitHubSourceKind.Issue, 42, "Widget is broken", "Details here", GitHubIssueAction.Opened, "https://github.com/acme/widgets/issues/42");

        await Run(message);

        _asanaClient.Verify(c => c.CreateTaskAsync("[Issue] Widget is broken", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _idMappingStore.Verify(s => s.SaveMappingAsync("acme/widgets", GitHubSourceKind.Issue, 42, "task-123", It.IsAny<CancellationToken>()), Times.Once);
        _asanaClient.Verify(c => c.UpdateTaskAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_ExistingIssueClosed_CompletesTaskWithoutCreatingANewOne()
    {
        _idMappingStore
            .Setup(s => s.FindAsanaTaskGidAsync("acme/widgets", GitHubSourceKind.Issue, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-123");

        var message = new SyncMessage("acme/widgets", GitHubSourceKind.Issue, 42, "Widget is broken", "Details here", GitHubIssueAction.Closed, "https://github.com/acme/widgets/issues/42");

        await Run(message);

        _asanaClient.Verify(c => c.CreateTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _asanaClient.Verify(c => c.UpdateTaskAsync("task-123", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_ClosedIssueNeverSeenBefore_CreatesTaskThenCompletesIt()
    {
        // Self-healing case: the sync went live after this issue was already
        // closed, so there is no mapping yet — the task should still end up
        // created AND marked complete, not stuck open.
        _idMappingStore
            .Setup(s => s.FindAsanaTaskGidAsync("acme/widgets", GitHubSourceKind.Issue, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _asanaClient
            .Setup(c => c.CreateTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-999");

        var message = new SyncMessage("acme/widgets", GitHubSourceKind.Issue, 7, "Old issue", "Body", GitHubIssueAction.Closed, "https://github.com/acme/widgets/issues/7");

        await Run(message);

        _asanaClient.Verify(c => c.CreateTaskAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _asanaClient.Verify(c => c.UpdateTaskAsync("task-999", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_ExistingIssueEdited_AddsACommentInsteadOfChangingCompletion()
    {
        _idMappingStore
            .Setup(s => s.FindAsanaTaskGidAsync("acme/widgets", GitHubSourceKind.Issue, 42, It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-123");

        var message = new SyncMessage("acme/widgets", GitHubSourceKind.Issue, 42, "Widget is broken", "Updated details", GitHubIssueAction.Edited, "https://github.com/acme/widgets/issues/42");

        await Run(message);

        _asanaClient.Verify(c => c.AddCommentAsync("task-123", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        _asanaClient.Verify(c => c.UpdateTaskAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_NewPullRequestOpened_CreatesTaskTaggedAsPr()
    {
        _idMappingStore
            .Setup(s => s.FindAsanaTaskGidAsync("acme/widgets", GitHubSourceKind.PullRequest, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _asanaClient
            .Setup(c => c.CreateTaskAsync("[PR] Fix the widget", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-555");

        var message = new SyncMessage("acme/widgets", GitHubSourceKind.PullRequest, 5, "Fix the widget", "Body", GitHubIssueAction.Opened, "https://github.com/acme/widgets/pull/5");

        await Run(message);

        _asanaClient.Verify(c => c.CreateTaskAsync("[PR] Fix the widget", It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_PullRequestMerged_CommentsThatItMergedThenCompletesTask()
    {
        _idMappingStore
            .Setup(s => s.FindAsanaTaskGidAsync("acme/widgets", GitHubSourceKind.PullRequest, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-555");

        var message = new SyncMessage("acme/widgets", GitHubSourceKind.PullRequest, 5, "Fix the widget", "Body", GitHubIssueAction.Closed, "https://github.com/acme/widgets/pull/5", Merged: true);

        await Run(message);

        _asanaClient.Verify(c => c.AddCommentAsync("task-555", It.Is<string>(text => text.Contains("Merged")), It.IsAny<CancellationToken>()), Times.Once);
        _asanaClient.Verify(c => c.UpdateTaskAsync("task-555", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_PullRequestClosedWithoutMerging_CommentsThatItClosedUnmergedThenCompletesTask()
    {
        _idMappingStore
            .Setup(s => s.FindAsanaTaskGidAsync("acme/widgets", GitHubSourceKind.PullRequest, 6, It.IsAny<CancellationToken>()))
            .ReturnsAsync("task-556");

        var message = new SyncMessage("acme/widgets", GitHubSourceKind.PullRequest, 6, "Abandoned change", "Body", GitHubIssueAction.Closed, "https://github.com/acme/widgets/pull/6", Merged: false);

        await Run(message);

        _asanaClient.Verify(c => c.AddCommentAsync("task-556", It.Is<string>(text => text.Contains("without merging")), It.IsAny<CancellationToken>()), Times.Once);
        _asanaClient.Verify(c => c.UpdateTaskAsync("task-556", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    private Task Run(SyncMessage message) =>
        _sut.Run(System.Text.Json.JsonSerializer.Serialize(message), CancellationToken.None);
}
