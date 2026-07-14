using System.Security.Cryptography;
using System.Text;

namespace OrizonAgents.Infrastructure.WhatsApp;

public static class WhatsAppSecurity
{
    public static bool VerifySignature(string rawBody, string? signatureHeader, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(appSecret)) return false;
        const string prefix = "sha256=";
        if (!signatureHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;

        string providedHex = signatureHeader[prefix.Length..];
        byte[] provided = ConvertHex(providedHex);
        if (provided.Length == 0) return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        byte[] computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return provided.Length == computed.Length && CryptographicOperations.FixedTimeEquals(provided, computed);
    }

    public static string ComputeSignature(string rawBody, string appSecret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        return "sha256=" + Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody))).ToLowerInvariant();
    }

    private static byte[] ConvertHex(string value)
    {
        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return Array.Empty<byte>();
        }
    }
}
