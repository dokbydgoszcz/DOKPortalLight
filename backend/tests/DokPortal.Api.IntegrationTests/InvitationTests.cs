using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using DokPortal.Application.Auth;
using DokPortal.Application.Users;
using DokPortal.Api.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Zaproszenie: konto bez hasła, e-mail z linkiem „Ustaw hasło”, ważnym 7 dni i działającym raz.</summary>
public class InvitationTests : IntegrationTestBase, IClassFixture<RecordingEmailFactory>
{
    private readonly RecordingEmailFactory _emailFactory;

    public InvitationTests(CustomWebApplicationFactory unused, RecordingEmailFactory factory) : base(factory) => _emailFactory = factory;

    private Task<HttpClient> AdminAsync() =>
        CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

    private (string To, string Subject, string Body) LastMailTo(string email)
    {
        lock (_emailFactory.Sender.Sent) return _emailFactory.Sender.Sent.Last(m => m.To == email);
    }

    private static (string UserId, string Token) LinkFrom(string body)
    {
        var match = Regex.Match(body, @"reset-hasla\?uid=([^&\s]+)&token=([^&\s]+)&zaproszenie=1");
        Assert.True(match.Success, body);
        return (Uri.UnescapeDataString(match.Groups[1].Value), Uri.UnescapeDataString(match.Groups[2].Value));
    }

    [Fact]
    public async Task AnAccountWithoutAPassword_GetsAnInvitation_AndSetsItsOwnPasswordOnce()
    {
        var admin = await AdminAsync();
        var email = $"zaproszony-{Guid.NewGuid():N}@example.org";

        var create = await admin.PostAsJsonAsync("/api/users", new { Email = email, Roles = new[] { "DyrektorDOK" } });
        var created = (await create.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<UserDto>())!;
        Assert.False(created.HasPassword);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "cokolwiek1" })).StatusCode);

        var invite = await admin.PostAsync($"/api/users/{created.Id}/invitation", null);
        Assert.Equal(HttpStatusCode.NoContent, invite.StatusCode);

        var mail = LastMailTo(email);
        Assert.Equal("Zaproszenie do DOK Portal", mail.Subject);
        Assert.Contains(email, mail.Body);
        Assert.Contains("7 dni", mail.Body);
        var (userId, token) = LinkFrom(mail.Body);

        var weak = await Client.PostAsJsonAsync("/api/auth/reset-password", new { UserId = userId, Token = token, NewPassword = "krotkie" });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        // Słabe hasło nie zużywa linku.
        var set = await Client.PostAsJsonAsync("/api/auth/reset-password", new { UserId = userId, Token = token, NewPassword = "MojeHaslo12!" });
        Assert.True(set.IsSuccessStatusCode);
        var reused = await Client.PostAsJsonAsync("/api/auth/reset-password", new { UserId = userId, Token = token, NewPassword = "Inne12345!" });
        Assert.Equal(HttpStatusCode.BadRequest, reused.StatusCode);

        var login = await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "MojeHaslo12!" });
        var session = (await login.EnsureSuccessStatusCode().Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.False(session.MustChangePassword);

        var listed = (await admin.GetFromJsonAsync<List<UserDto>>("/api/users"))!.Single(u => u.Id == created.Id);
        Assert.True(listed.HasPassword);
    }

    [Fact]
    public async Task TheAdminCanResendTheLink_ToAnAccountThatAlreadyHasAPassword()
    {
        var admin = await AdminAsync();
        var email = $"zapomnial-{Guid.NewGuid():N}@example.org";
        var created = (await (await admin.PostAsJsonAsync("/api/users", new { Email = email, Password = "Tymczasowe1!", Roles = new[] { "DyrektorDOK" } }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<UserDto>())!;
        Assert.True(created.HasPassword);

        (await admin.PostAsync($"/api/users/{created.Id}/invitation", null)).EnsureSuccessStatusCode();
        var (userId, token) = LinkFrom(LastMailTo(email).Body);
        (await Client.PostAsJsonAsync("/api/auth/reset-password", new { UserId = userId, Token = token, NewPassword = "Swoje12345!" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Tymczasowe1!" })).StatusCode);
        var login = (await (await Client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = "Swoje12345!" })).Content.ReadFromJsonAsync<LoginResponse>())!;
        Assert.False(login.MustChangePassword);
    }

    [Fact]
    public async Task NoInvitation_ForUnknownOrDisabledAccounts()
    {
        var admin = await AdminAsync();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/users/{Guid.NewGuid()}/invitation", null)).StatusCode);

        var email = $"wylaczony-{Guid.NewGuid():N}@example.org";
        var created = (await (await admin.PostAsJsonAsync("/api/users", new { Email = email, Roles = Array.Empty<string>() }))
            .EnsureSuccessStatusCode().Content.ReadFromJsonAsync<UserDto>())!;
        (await admin.PutAsJsonAsync($"/api/users/{created.Id}/active", new { IsActive = false })).EnsureSuccessStatusCode();

        var invite = await admin.PostAsync($"/api/users/{created.Id}/invitation", null);
        Assert.Equal(HttpStatusCode.BadRequest, invite.StatusCode);
        Assert.Contains("wyłączone", await invite.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AShortTemporaryPassword_IsStillRejected()
    {
        var admin = await AdminAsync();
        var create = await admin.PostAsJsonAsync("/api/users", new { Email = $"k-{Guid.NewGuid():N}@example.org", Password = "abc", Roles = Array.Empty<string>() });
        Assert.Equal(HttpStatusCode.BadRequest, create.StatusCode);
    }

    [Fact]
    public void TheInvitationLink_IsValidForSevenDays()
    {
        var options = Factory.Services.GetRequiredService<IOptions<InvitationTokenProviderOptions>>().Value;
        Assert.Equal(TimeSpan.FromDays(7), options.TokenLifespan);
    }
}
