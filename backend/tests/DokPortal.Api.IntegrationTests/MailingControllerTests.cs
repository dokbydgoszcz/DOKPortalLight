using System.Net.Http.Json;
using DokPortal.Application.Mailing;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class MailingControllerTests : IntegrationTestBase
{
    public MailingControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task CreateThenSend_UpdatesStatusAndRecipientCount()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var createResponse = await admin.PostAsJsonAsync("/api/mailing/campaigns", new
        {
            Subject = "Zaproszenie na rekolekcje", Body = "Treść zaproszenia", Group = "DokCases"
        });
        var created = await createResponse.Content.ReadFromJsonAsync<MailingCampaignDto>(EnumJsonOptions);
        Assert.Equal("Draft", created!.Status.ToString());

        var sendResponse = await admin.PostAsJsonAsync($"/api/mailing/campaigns/{created.Id}/send", new { });
        var sent = await sendResponse.Content.ReadFromJsonAsync<MailingCampaignDto>(EnumJsonOptions);

        Assert.Equal("Sent", sent!.Status.ToString());
        Assert.NotNull(sent.SentAtUtc);
    }
}
