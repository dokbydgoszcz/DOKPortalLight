using System.Net;
using System.Net.Http.Json;
using DokPortal.Application.People;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Import wklejonej listy osób przez API – na prawdziwej bazie.</summary>
public class PeopleImportEndpointsTests : IntegrationTestBase
{
    public PeopleImportEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public async Task APeopleManager_PreviewsAndImports_ACatechistCannot()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var catechist = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "Katechista");
        var last = "Importowy" + new string(Guid.NewGuid().ToString("N")[..8].Select(c => (char)(char.IsDigit(c) ? c - '0' + 'g' : c)).ToArray());
        var body = new { Text = $"Rok III\n1 {last} Jan\n2 {last} Ewa", EnrollAsCandidates = true, Function = "Catechist", FunctionNote = "test" };

        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.PostAsJsonAsync("/api/people/import/preview", body)).StatusCode);

        var previewResponse = await admin.PostAsJsonAsync("/api/people/import/preview", body);
        Assert.True(previewResponse.IsSuccessStatusCode, await previewResponse.Content.ReadAsStringAsync());
        var preview = await previewResponse.Content.ReadFromJsonAsync<PeopleImportResultDto>(EnumJsonOptions);
        Assert.Equal((false, 2, 2, 2), (preview!.Applied, preview.NewPeople, preview.FunctionsGranted, preview.CandidatesEnrolled));

        var done = await (await admin.PostAsJsonAsync("/api/people/import", body)).Content.ReadFromJsonAsync<PeopleImportResultDto>(EnumJsonOptions);
        Assert.True(done!.Applied);
        var again = await (await admin.PostAsJsonAsync("/api/people/import/preview", body)).Content.ReadFromJsonAsync<PeopleImportResultDto>(EnumJsonOptions);
        Assert.Equal((0, 2, 0, 0), (again!.NewPeople, again.ExistingPeople, again.FunctionsGranted, again.CandidatesEnrolled));

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/people/import/preview", new { Text = "" })).StatusCode);
    }
}
