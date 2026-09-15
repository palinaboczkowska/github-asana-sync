using System.Security.Cryptography;
using System.Text;
using GithubAsanaSync.Functions.Services;
using Xunit;

namespace GithubAsanaSync.Tests;

public class GitHubWebhookValidatorTests
{
    private const string Secret = "test-secret";
    private readonly GitHubWebhookValidator _validator = new();

    [Fact]
    public void IsValidSignature_WithCorrectSignature_ReturnsTrue()
    {
        var body = "{\"action\":\"opened\"}";
        var signature = ComputeSignatureHeader(body, Secret);

        var result = _validator.IsValidSignature(body, signature, Secret);

        Assert.True(result);
    }

    [Fact]
    public void IsValidSignature_WithWrongSecret_ReturnsFalse()
    {
        var body = "{\"action\":\"opened\"}";
        var signature = ComputeSignatureHeader(body, "a-different-secret");

        var result = _validator.IsValidSignature(body, signature, Secret);

        Assert.False(result);
    }

    [Fact]
    public void IsValidSignature_WithTamperedBody_ReturnsFalse()
    {
        var signature = ComputeSignatureHeader("{\"action\":\"opened\"}", Secret);

        var result = _validator.IsValidSignature("{\"action\":\"closed\"}", signature, Secret);

        Assert.False(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-even-hex-formatted")]
    [InlineData("sha1=deadbeef")]
    public void IsValidSignature_WithMissingOrMalformedHeader_ReturnsFalse(string? header)
    {
        var result = _validator.IsValidSignature("{}", header, Secret);

        Assert.False(result);
    }

    private static string ComputeSignatureHeader(string body, string secret)
    {
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(body));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
