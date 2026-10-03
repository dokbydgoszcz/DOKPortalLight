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

    [Theory]
    [InlineData("", "Treść", "Subject", "Podaj temat kampanii.")]
    [InlineData("   ", "Treść", "Subject", "Podaj temat kampanii.")]
    [InlineData("Temat", "", "Body", "Podaj treść kampanii.")]
    [InlineData("Temat", "  \n ", "Body", "Podaj treść kampanii.")]
    public async Task Create_WithoutSubjectOrBody_IsRejected_AndNothingIsSaved(string subject, string body, string field, string message)
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var before = (await admin.GetFromJsonAsync<List<MailingCampaignDto>>("/api/mailing/campaigns", EnumJsonOptions))!.Count;

        var response = await admin.PostAsJsonAsync("/api/mailing/campaigns", new { Subject = subject, Body = body, Group = "DokCases" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadAsStringAsync();
        Assert.Contains(field, problem);
        Assert.Contains(message, problem);
        var after = (await admin.GetFromJsonAsync<List<MailingCampaignDto>>("/api/mailing/campaigns", EnumJsonOptions))!.Count;
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Create_RejectsATooLongSubject_AndAnUnknownGroup()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var tooLong = await admin.PostAsJsonAsync("/api/mailing/campaigns", new { Subject = new string('x', 201), Body = "Treść", Group = "DokCases" });
        var unknownGroup = await admin.PostAsJsonAsync("/api/mailing/campaigns", new { Subject = "Temat", Body = "Treść", Group = 99 });
        var exactlyAtTheLimit = await admin.PostAsJsonAsync("/api/mailing/campaigns", new { Subject = new string('x', 200), Body = "Treść", Group = "DokCases" });

        Assert.Equal(System.Net.HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, unknownGroup.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.OK, exactlyAtTheLimit.StatusCode);
    }
}
