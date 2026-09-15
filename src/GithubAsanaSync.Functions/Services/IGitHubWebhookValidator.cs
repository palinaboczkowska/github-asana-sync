namespace GithubAsanaSync.Functions.Services;

public interface IGitHubWebhookValidator
{
    // GitHub signs each webhook body with HMAC-SHA256 using the secret
    // configured on the webhook. The signature arrives in the
    // "X-Hub-Signature-256" header as "sha256=<hex digest>".
    bool IsValidSignature(string payloadBody, string? signatureHeader, string secret);
}
