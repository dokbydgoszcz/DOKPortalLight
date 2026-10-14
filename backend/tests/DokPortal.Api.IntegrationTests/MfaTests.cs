using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using DokPortal.Application.Auth;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Weryfikacja dwuetapowa (MFA) aplikacją typu Microsoft/Google Authenticator: włączenie z kodem QR, logowanie z kodem, kody zapasowe, wyłączenie.</summary>
public class MfaTests : IntegrationTestBase, IClassFixture<RecordingEmailFactory>
{
    private readonly RecordingEmailFactory _emailFactory;

    public MfaTests(CustomWebApplicationFactory unused, RecordingEmailFactory factory) : base(factory) => _emailFactory = factory;

    /// <summary>Kod TOTP (RFC 6238, SHA1, 6 cyfr, 30 s) – tak jak liczy go aplikacja na telefonie.</summary>
    private static string Totp(string base32Key, DateTimeOffset? at = null)
    {
        var key = FromBase32(base32Key);
        var counter = (at ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / 30;
        var bytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(bytes);
        var hash = new HMACSHA1(key).ComputeHash(bytes);
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] FromBase32(string input)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var clean = input.Replace(" ", "").TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in clean)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                output.Add((byte)(buffer >> (bits - 8)));
                bits -= 8;
            }
        }
        return output.ToArray();
    }

    private HttpClient ClientWith(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<JsonElement> LoginAsync(string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    /// <summary>Włącza MFA na koncie i zwraca klucz, kody zapasowe oraz nową sesję.</summary>
    private async Task<(string Key, IReadOnlyList<string> RecoveryCodes, string Token)> EnableAsync(string email)
    {
        var token = await CreateUserAndGetTokenAsync(email, "Sekret123!", "DyrektorDOK");
        var client = ClientWith(token);
        var setup = (await (await client.PostAsync("/api/auth/mfa/setup", null)).EnsureSuccessStatusCode()
            .Content.ReadFromJsonAsync<MfaSetupResponse>())!;
        var enable = await client.PostAsJsonAsync("/api/auth/mfa/enable", new { Code = Totp(setup.SharedKey) });
        enable.EnsureSuccessStatusCode();
        var enabled = (await enable.Content.ReadFromJsonAsync<MfaEnabledResponse>())!;
        return (setup.SharedKey, enabled.RecoveryCodes, enabled.Session.Token);
    }

    [Fact]
    public async Task Setup_GivesAQrLinkForTheAuthenticatorApp_AndEnablingNeedsAValidCode()
    {
        var email = $"mfa-{Guid.NewGuid():N}@example.org";
        var client = ClientWith(await CreateUserAndGetTokenAsync(email, "Sekret123!", "DyrektorDOK"));

        var setup = (await (await client.PostAsync("/api/auth/mfa/setup", null)).Content.ReadFromJsonAsync<MfaSetupResponse>())!;
        var wrong = await client.PostAsJsonAsync("/api/auth/mfa/enable", new { Code = "000000" });
        var status = await client.GetFromJsonAsync<MfaStatusResponse>("/api/auth/mfa");

        Assert.StartsWith("otpauth://totp/DOK%20Portal:", setup.OtpAuthUri);
        Assert.Contains($"secret={setup.SharedKey}", setup.OtpAuthUri);
        Assert.Contains("issuer=DOK%20Portal", setup.OtpAuthUri);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("Nieprawidłowy kod", await wrong.Content.ReadAsStringAsync());
        Assert.False(status!.Enabled);
        Assert.True((await LoginAsync(email, "Sekret123!")).TryGetProperty("token", out _));
    }

    [Fact]
    public async Task OnceEnabled_LoginNeedsTheCodeFromTheApp()
    {
        var email = $"mfa-{Guid.NewGuid():N}@example.org";
        var (key, codes, token) = await EnableAsync(email);

        Assert.Equal(10, codes.Count);
        Assert.Equal(HttpStatusCode.OK, (await ClientWith(token).GetAsync("/api/dok-cases")).StatusCode);
        Assert.Contains(_emailFactory.Sender.Sent, m => m.To == email && m.Subject == "Weryfikacja dwuetapowa włączona");

        var first = await LoginAsync(email, "Sekret123!");
        Assert.True(first.GetProperty("mfaRequired").GetBoolean());
        Assert.False(first.TryGetProperty("token", out _));
        var mfaToken = first.GetProperty("mfaToken").GetString();

        var wrong = await Client.PostAsJsonAsync("/api/auth/mfa/verify", new { MfaToken = mfaToken, Code = "123456" });
        var forged = await Client.PostAsJsonAsync("/api/auth/mfa/verify", new { MfaToken = "falszywy", Code = Totp(key) });
        var ok = await Client.PostAsJsonAsync("/api/auth/mfa/verify", new { MfaToken = mfaToken, Code = Totp(key) });

        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Contains("Nieprawidłowy kod", await wrong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var session = (await ok.Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.Equal(HttpStatusCode.OK, (await ClientWith(session.Token).GetAsync("/api/dok-cases")).StatusCode);
    }

    [Fact]
    public async Task ARecoveryCode_ReplacesTheAppOnce()
    {
        var email = $"mfa-{Guid.NewGuid():N}@example.org";
        var (_, codes, _) = await EnableAsync(email);

        var token1 = (await LoginAsync(email, "Sekret123!")).GetProperty("mfaToken").GetString();
        var used = await Client.PostAsJsonAsync("/api/auth/mfa/verify", new { MfaToken = token1, Code = codes[0].Replace("-", "").ToLowerInvariant() });
        var token2 = (await LoginAsync(email, "Sekret123!")).GetProperty("mfaToken").GetString();
        var again = await Client.PostAsJsonAsync("/api/auth/mfa/verify", new { MfaToken = token2, Code = codes[0] });

        Assert.Equal(HttpStatusCode.OK, used.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, again.StatusCode);
    }

    [Fact]
    public async Task DisablingNeedsACode_AndTheUserIsTold()
    {
        var email = $"mfa-{Guid.NewGuid():N}@example.org";
        var (key, _, token) = await EnableAsync(email);
        var client = ClientWith(token);

        var wrong = await client.PostAsJsonAsync("/api/auth/mfa/disable", new { Code = "000000" });
        var disable = await client.PostAsJsonAsync("/api/auth/mfa/disable", new { Code = Totp(key) });
        var session = await disable.Content.ReadFromJsonAsync<LoginResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.OK, disable.StatusCode);
        Assert.False((await ClientWith(session!.Token).GetFromJsonAsync<MfaStatusResponse>("/api/auth/mfa"))!.Enabled);
        Assert.True((await LoginAsync(email, "Sekret123!")).TryGetProperty("token", out _));
        Assert.Contains(_emailFactory.Sender.Sent, m => m.To == email && m.Subject == "Weryfikacja dwuetapowa wyłączona");
    }

    [Fact]
    public async Task AnAdministrator_CanTurnItOff_WhenThePhoneIsLost()
    {
        var email = $"mfa-{Guid.NewGuid():N}@example.org";
        await EnableAsync(email);
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var user = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == email);
        var bishop = await CreateAuthenticatedClientAsync($"bp-{Guid.NewGuid():N}@example.org", "Sekret123!", "Biskup");

        Assert.True(user.MfaEnabled);
        Assert.Equal(HttpStatusCode.Forbidden, (await bishop.DeleteAsync($"/api/users/{user.Id}/mfa")).StatusCode);
        var reset = await admin.DeleteAsync($"/api/users/{user.Id}/mfa");

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.False((await reset.Content.ReadFromJsonAsync<UserDto>())!.MfaEnabled);
        Assert.True((await LoginAsync(email, "Sekret123!")).TryGetProperty("token", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await admin.DeleteAsync("/api/users/brak/mfa")).StatusCode);
    }

    [Fact]
    public async Task MfaEndpoints_NeedASession()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsync("/api/auth/mfa/setup", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/auth/mfa")).StatusCode);
    }

    [Fact]
    public async Task WithoutMfa_EveryThirdLoginSuggestsTurningItOn_AndWithMfaNever()
    {
        var email = $"zacheta-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Sekret123!", "DyrektorDOK");
        var hints = new List<bool>();
        for (var i = 0; i < 6; i++) hints.Add((await LoginAsync(email, "Sekret123!")).GetProperty("suggestMfa").GetBoolean());
        Assert.Equal(2, hints.Count(h => h));
        var first = hints.IndexOf(true);
        Assert.True(hints[first + 3]);
    }
}
