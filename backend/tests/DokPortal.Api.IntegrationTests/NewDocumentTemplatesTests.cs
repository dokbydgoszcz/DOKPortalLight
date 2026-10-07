using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.Documents;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Nowe typy pism: wniosek o misję, zaświadczenia o każdym sakramencie, studium katechumenalne, rodzice chrzestni, klauzula RODO.</summary>
public class NewDocumentTemplatesTests : IntegrationTestBase
{
    public NewDocumentTemplatesTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("CanonicalMissionApplication")]
    [InlineData("CatechumenateStudyCertificate")]
    [InlineData("BaptismCertificate")]
    [InlineData("ConfirmationCertificate")]
    [InlineData("EucharistCertificate")]
    [InlineData("GodparentCertificate")]
    [InlineData("GdprClause")]
    public async Task EachNewTemplate_GeneratesAPdf_AndLandsInTheHistoryWithItsName(string template)
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await SeedPersonAsync(admin, "Jan", $"Kowalski{Guid.NewGuid():N}".Substring(0, 14));

        var response = await admin.PostAsJsonAsync("/api/documents/generate", new { Template = template, PersonId = personId });
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        var history = await admin.GetFromJsonAsync<List<GeneratedDocumentDto>>("/api/documents", EnumJsonOptions);
        Assert.Contains(history!, d => d.PersonId == personId && d.Template.ToString() == template);
    }

    [Fact]
    public async Task TheOldGenericSacramentCertificate_CanNoLongerBeGenerated()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await SeedPersonAsync(admin, "Jan", $"Kowalski{Guid.NewGuid():N}".Substring(0, 14));

        var response = await admin.PostAsJsonAsync("/api/documents/generate", new { Template = "SacramentCertificate", PersonId = personId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("nie jest już dostępny", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task AnUnknownTemplateName_IsABadRequest()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var personId = await SeedPersonAsync(admin, "Jan", $"Kowalski{Guid.NewGuid():N}".Substring(0, 14));

        var response = await admin.PostAsJsonAsync("/api/documents/generate", new { Template = "Nieistniejace", PersonId = personId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
