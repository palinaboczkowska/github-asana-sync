using System.Text.Json;
using GithubAsanaSync.Functions.Functions;
using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Moq;
using Xunit;

namespace GithubAsanaSync.Tests;

public class ProcessSyncMessageTests
{
    private readonly Mock<IIssueSyncService> _issueSyncService = new();
    private readonly Mock<ITaskSyncService> _taskSyncService = new();
    private readonly ProcessSyncMessage _sut;

    public ProcessSyncMessageTests()
    {
        _sut = new ProcessSyncMessage(_issueSyncService.Object, _taskSyncService.Object);
    }

    [Fact]
    public async Task Run_GitHubMessage_HandsItToTheIssueSyncService()
    {
        var message = new SyncMessage("acme/widgets", GitHubSourceKind.Issue, 42, "Widget is broken", "Details here", GitHubIssueAction.Opened, "https://github.com/acme/widgets/issues/42");

        await _sut.Run(JsonSerializer.Serialize(message), CancellationToken.None);

        _issueSyncService.Verify(s => s.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        _taskSyncService.Verify(s => s.HandleAsync(It.IsAny<AsanaTaskMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_AsanaMessage_HandsItToTheTaskSyncService()
    {
        var message = new AsanaTaskMessage("task-123");

        await _sut.Run(JsonSerializer.Serialize(message), CancellationToken.None);

        _taskSyncService.Verify(s => s.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
        _issueSyncService.Verify(s => s.HandleAsync(It.IsAny<SyncMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_UnparseableMessage_ThrowsSoServiceBusRetriesAndDeadLetters()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Run("null", CancellationToken.None));

        _issueSyncService.Verify(s => s.HandleAsync(It.IsAny<SyncMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        _taskSyncService.Verify(s => s.HandleAsync(It.IsAny<AsanaTaskMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
