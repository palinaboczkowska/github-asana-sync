using System.Globalization;
using System.Security.Cryptography;
using System.Text;

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

        var expectedHex = signatureHeader[SignaturePrefix.Length..];
        if (!TryParseHex(expectedHex, out var expectedBytes))
        {
            return false;
        }

        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadBody);
        var actualBytes = HMACSHA256.HashData(keyBytes, payloadBytes);

        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static bool TryParseHex(string hex, out byte[] bytes)
    {
        if (hex.Length % 2 != 0)
        {
            bytes = [];
            return false;
        }

        bytes = new byte[hex.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            if (!byte.TryParse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, null, out bytes[i]))
            {
                return false;
            }
        }

        return true;
    }
}
