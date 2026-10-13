using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DokPortal.Application.Auth;
using DokPortal.Application.Users;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Hasła bez pośrednictwa administratora: zmiana własnego, wymuszenie przy pierwszym logowaniu, „Nie pamiętam hasła”.</summary>
public class PasswordSelfServiceTests : IntegrationTestBase, IClassFixture<RecordingEmailFactory>
{
    private readonly RecordingEmailFactory _emailFactory;

    public PasswordSelfServiceTests(CustomWebApplicationFactory unused, RecordingEmailFactory factory) : base(factory) => _emailFactory = factory;

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private async Task<HttpResponseMessage> LoginAsync(string email, string password) =>
        await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });

    private HttpClient ClientWith(string token)
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<UserDto> CreateViaAdminAsync(HttpClient admin, string email, string password) =>
        (await (await admin.PostAsJsonAsync("/api/users", new { Email = email, Password = password, Roles = new[] { "DyrektorDOK" } }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<UserDto>())!;

    [Fact]
    public async Task AnAccountCreatedByTheAdmin_MustChangeThePassword_AndAfterTheChangeTheOldOneStopsWorking()
    {
        var admin = await AdminAsync();
        var email = $"nowy-{Guid.NewGuid():N}@example.org";
        await CreateViaAdminAsync(admin, email, "Tymczasowe1!");

        var first = (await (await LoginAsync(email, "Tymczasowe1!")).Content.ReadFromJsonAsync<LoginResponse>())!;
        var session = ClientWith(first.Token);
        var wrong = await session.PostAsJsonAsync("/api/auth/change-password", new { CurrentPassword = "zle-haslo", NewPassword = "MojeWlasne1!" });
        var same = await session.PostAsJsonAsync("/api/auth/change-password", new { CurrentPassword = "Tymczasowe1!", NewPassword = "Tymczasowe1!" });
        var change = await session.PostAsJsonAsync("/api/auth/change-password", new { CurrentPassword = "Tymczasowe1!", NewPassword = "MojeWlasne1!" });
        var changed = (await change.Content.ReadFromJsonAsync<LoginResponse>())!;

        Assert.True(first.MustChangePassword);
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("Obecne hasło", await wrong.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.Equal(HttpStatusCode.OK, change.StatusCode);
        Assert.False(changed.MustChangePassword);
        Assert.Equal(HttpStatusCode.OK, (await ClientWith(changed.Token).GetAsync("/api/dok-cases")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/dok-cases")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(email, "Tymczasowe1!")).StatusCode);
        var again = (await (await LoginAsync(email, "MojeWlasne1!")).Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.False(again.MustChangePassword);
    }

    [Fact]
    public async Task APasswordResetByTheAdmin_MustBeChangedAgain()
    {
        var admin = await AdminAsync();
        var email = $"reset-{Guid.NewGuid():N}@example.org";
        var user = await CreateViaAdminAsync(admin, email, "Tymczasowe1!");
        var token = (await (await LoginAsync(email, "Tymczasowe1!")).Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        (await ClientWith(token).PostAsJsonAsync("/api/auth/change-password", new { CurrentPassword = "Tymczasowe1!", NewPassword = "MojeWlasne1!" })).EnsureSuccessStatusCode();

        (await admin.PutAsJsonAsync($"/api/users/{user.Id}/reset-password", new { NewPassword = "OdAdmina12!" })).EnsureSuccessStatusCode();
        var login = (await (await LoginAsync(email, "OdAdmina12!")).Content.ReadFromJsonAsync<LoginResponse>())!;

        Assert.True(login.MustChangePassword);
    }

    [Fact]
    public async Task ChangingThePassword_RequiresBeingLoggedIn()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/change-password", new { CurrentPassword = "a", NewPassword = "Bbbbbbbb1!" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_SendsALinkThatSetsANewPassword_Once()
    {
        var email = $"zapomnial-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Stare1234!", "DyrektorDOK");

        var forgot = await Client.PostAsJsonAsync("/api/auth/forgot-password", new { Email = email });
        var mail = Assert.Single(_emailFactory.Sender.Sent, m => m.To == email);
        var match = Regex.Match(mail.Body, @"reset-hasla\?uid=([^&\s]+)&token=([^\s]+)");
        Assert.True(match.Success, mail.Body);
        var userId = Uri.UnescapeDataString(match.Groups[1].Value);
        var token = Uri.UnescapeDataString(match.Groups[2].Value);

        var reset = await Client.PostAsJsonAsync("/api/auth/reset-password", new { UserId = userId, Token = token, NewPassword = "Nowe12345!" });
        var reused = await Client.PostAsJsonAsync("/api/auth/reset-password", new { UserId = userId, Token = token, NewPassword = "Inne12345!" });

        Assert.Equal(HttpStatusCode.OK, forgot.StatusCode);
        Assert.Equal("Zmiana hasła w DOK Portal", mail.Subject);
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);
        Assert.Contains("wygasł", await reused.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync(email, "Stare1234!")).StatusCode);
        var login = (await (await LoginAsync(email, "Nowe12345!")).Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.False(login.MustChangePassword);
    }

    [Fact]
    public async Task ForgotPassword_AnswersTheSameForUnknownAndDeactivatedAccounts_AndSendsNothing()
    {
        var admin = await AdminAsync();
        var email = $"wylaczony-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Stare1234!", "DyrektorDOK");
        var user = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == email);
        (await admin.PutAsJsonAsync($"/api/users/{user.Id}/active", new { IsActive = false })).EnsureSuccessStatusCode();
        var unknown = $"nikt-{Guid.NewGuid():N}@example.org";

        var a = await Client.PostAsJsonAsync("/api/auth/forgot-password", new { Email = unknown });
        var b = await Client.PostAsJsonAsync("/api/auth/forgot-password", new { Email = email });

        Assert.Equal(HttpStatusCode.OK, a.StatusCode);
        Assert.Equal(await a.Content.ReadAsStringAsync(), await b.Content.ReadAsStringAsync());
        Assert.DoesNotContain(_emailFactory.Sender.Sent, m => m.To == email || m.To == unknown);
    }

    [Fact]
    public async Task AForgedResetToken_IsRejected()
    {
        var email = $"falsz-{Guid.NewGuid():N}@example.org";
        await CreateUserAndGetTokenAsync(email, "Stare1234!", "DyrektorDOK");
        var admin = await AdminAsync();
        var user = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Email == email);

        var reset = await Client.PostAsJsonAsync("/api/auth/reset-password", new { UserId = user.Id, Token = "nieprawdziwy", NewPassword = "Nowe12345!" });
        var noUser = await Client.PostAsJsonAsync("/api/auth/reset-password", new { UserId = "brak", Token = "x", NewPassword = "Nowe12345!" });

        Assert.Equal(HttpStatusCode.BadRequest, reset.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noUser.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(email, "Stare1234!")).StatusCode);
    }
}
