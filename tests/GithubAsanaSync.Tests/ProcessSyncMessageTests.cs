using System.Text.Json;
using GithubAsanaSync.Functions.Functions;
using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Moq;
using Xunit;

namespace GithubAsanaSync.Tests;

public class ProcessSyncMessageTests
{
    [Fact]
    public async Task Run_ValidMessage_HandsItToTheSyncService()
    {
        var syncService = new Mock<IIssueSyncService>();
        var sut = new ProcessSyncMessage(syncService.Object);
        var message = new SyncMessage("acme/widgets", GitHubSourceKind.Issue, 42, "Widget is broken", "Details here", GitHubIssueAction.Opened, "https://github.com/acme/widgets/issues/42");

        await sut.Run(JsonSerializer.Serialize(message), CancellationToken.None);

        syncService.Verify(s => s.HandleAsync(message, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_UnparseableMessage_ThrowsSoServiceBusRetriesAndDeadLetters()
    {
        var syncService = new Mock<IIssueSyncService>();
        var sut = new ProcessSyncMessage(syncService.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.Run("null", CancellationToken.None));

        syncService.Verify(s => s.HandleAsync(It.IsAny<SyncMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
