using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GithubAsanaSync.Functions.Functions;
using GithubAsanaSync.Functions.Models;
using GithubAsanaSync.Functions.Services;
using Xunit;

namespace GithubAsanaSync.Tests;

public class AsanaWebhookTests
{
    private const string Secret = "handshake-secret";
    private const string Body = """{"events":[{"action":"changed","resource":{"gid":"111","resource_type":"task"}}]}""";

    private readonly AsanaWebhookValidator _validator = new();

    [Fact]
    public void Validator_CorrectSignature_IsAccepted()
    {
        Assert.True(_validator.IsValidSignature(Body, Sign(Body, Secret), Secret));
    }

    [Fact]
    public void Validator_TamperedBody_IsRejected()
    {
        var signature = Sign(Body, Secret);

        Assert.False(_validator.IsValidSignature(Body.Replace("111", "222"), signature, Secret));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-hex")]
    public void Validator_MissingOrMalformedSignature_IsRejected(string? signature)
    {
        Assert.False(_validator.IsValidSignature(Body, signature, Secret));
    }

    [Fact]
    public void Parse_SeveralEventsForSameTask_GivesOneMessagePerTask()
    {
        const string body = """
            {"events":[
              {"action":"changed","resource":{"gid":"111","resource_type":"task"}},
              {"action":"changed","resource":{"gid":"111","resource_type":"task"}},
              {"action":"changed","resource":{"gid":"222","resource_type":"task"}},
              {"action":"added","resource":{"gid":"333","resource_type":"story"}}
            ]}
            """;

        var messages = ReceiveAsanaWebhook.ParseTaskMessages(body)
            .Select(m => JsonSerializer.Deserialize<AsanaTaskMessage>(m)!.AsanaTaskGid)
            .ToArray();

        Assert.Equal(["111", "222"], messages);
    }

    [Fact]
    public void Parse_Heartbeat_GivesNoMessages()
    {
        Assert.Empty(ReceiveAsanaWebhook.ParseTaskMessages("""{"events":[]}"""));
    }

    private static string Sign(string body, string secret) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body)));
}
