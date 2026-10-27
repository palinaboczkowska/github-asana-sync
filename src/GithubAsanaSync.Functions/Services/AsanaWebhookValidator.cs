namespace GithubAsanaSync.Functions.Services;

public interface IAsanaWebhookValidator
{
    // Asana signs each event delivery with HMAC-SHA256 using the secret it
    // handed over during the webhook handshake. The signature arrives in the
    // "X-Hook-Signature" header as a bare hex digest (no prefix).
    bool IsValidSignature(string payloadBody, string? signatureHeader, string secret);
}

public sealed class AsanaWebhookValidator : IAsanaWebhookValidator
{
    public bool IsValidSignature(string payloadBody, string? signatureHeader, string secret) =>
        !string.IsNullOrEmpty(signatureHeader) && HmacSha256Signature.Matches(payloadBody, signatureHeader, secret);
}
