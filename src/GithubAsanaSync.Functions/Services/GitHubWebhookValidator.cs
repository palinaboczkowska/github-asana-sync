namespace GithubAsanaSync.Functions.Services;

public sealed class GitHubWebhookValidator : IGitHubWebhookValidator
{
    private const string SignaturePrefix = "sha256=";

    public bool IsValidSignature(string payloadBody, string? signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith(SignaturePrefix, StringComparison.Ordinal))
        {
            return false;
        }

        return HmacSha256Signature.Matches(payloadBody, signatureHeader[SignaturePrefix.Length..], secret);
    }
}
