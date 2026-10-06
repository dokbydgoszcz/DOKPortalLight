using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.AuditLog;
using DokPortal.Domain.Enums;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>„Wyślij wiadomość testową”: sprawdza, że konfiguracja SMTP działa, wysyłając wiadomość na adres zalogowanego użytkownika.</summary>
public class MailingTestEmailTests : IntegrationTestBase, IClassFixture<RecordingEmailFactory>
{
    private readonly RecordingEmailFactory _emailFactory;

    public MailingTestEmailTests(CustomWebApplicationFactory unused, RecordingEmailFactory factory) : base(factory) => _emailFactory = factory;

    [Fact]
    public async Task SendsTheTestMessageToTheLoggedInUsersOwnAddress_AndIsAudited()
    {
        var email = $"dyr-{Guid.NewGuid():N}@example.org";
        var director = await CreateAuthenticatedClientAsync(email, "Sekret123!", "DyrektorDOK");
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var response = await director.PostAsync("/api/mailing/test-email", null);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(email, body!["sentTo"]);
        Assert.Contains(_emailFactory.Sender.Sent, m => m.To == email && m.Subject == "Wiadomość testowa z DOK Portal");
        var log = (await admin.GetFromJsonAsync<List<AuditLogEntryDto>>("/api/audit-log?action=SendTestEmail", EnumJsonOptions))!;
        Assert.Contains(log, e => e.ObjectDescription == email && e.Result == AuditResult.Allowed);
    }

    [Fact]
    public async Task OnlyThoseWhoManageMailing_CanSendIt()
    {
        var catechist = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");
        var bishop = await CreateAuthenticatedClientAsync($"bp-{Guid.NewGuid():N}@example.org", "Sekret123!", "Biskup");

        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.PostAsync("/api/mailing/test-email", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await bishop.PostAsync("/api/mailing/test-email", null)).StatusCode);
    }
}
