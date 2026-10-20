using System.Security.Cryptography;
using System.Text;

namespace DokPortal.Api.Integrations;

/// <summary>Podpis webhooka Tally: nagłówek Tally-Signature = base64(HMAC-SHA256(sekret, treść żądania)).</summary>
public static class TallySignature
{
    public const string Header = "Tally-Signature";

    public static string Compute(string secret, byte[] body) =>
        Convert.ToBase64String(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body));

    public static bool IsValid(string secret, byte[] body, string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;
        var expected = Encoding.ASCII.GetBytes(Compute(secret, body));
        var actual = Encoding.ASCII.GetBytes(signature.Trim());
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
