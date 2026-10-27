using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GithubAsanaSync.Functions.Services;

// GitHub and Asana both sign webhook bodies with HMAC-SHA256 and send the
// digest as hex; they only differ in header name and GitHub's "sha256="
// prefix. The comparison is constant-time so the secret cannot be guessed
// from response timing.
internal static class HmacSha256Signature
{
    public static bool Matches(string payloadBody, string expectedHex, string secret)
    {
        if (!TryParseHex(expectedHex, out var expectedBytes))
        {
            return false;
        }

        var actualBytes = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(payloadBody));
        return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
    }

    private static bool TryParseHex(string hex, out byte[] bytes)
    {
        if (hex.Length == 0 || hex.Length % 2 != 0)
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
