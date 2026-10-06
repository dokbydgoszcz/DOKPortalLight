using System.Net;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class MailingTestEmailNotConfiguredTests : IntegrationTestBase
{
    public MailingTestEmailNotConfiguredTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task WithoutSmtp_ItIsABadRequestThatSaysSo()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");

        var response = await admin.PostAsync("/api/mailing/test-email", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("nie jest skonfigurowane", await response.Content.ReadAsStringAsync());
    }
}
