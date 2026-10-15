using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using DokPortal.Application.Parishes;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Import spisu parafii przez API (podgląd i zapis) – na prawdziwej bazie.</summary>
public class ParishImportEndpointsTests : IntegrationTestBase
{
    public ParishImportEndpointsTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    private static MultipartFormDataContent Workbook(string city)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("P");
        string[] header = { "Dekanat", "Miejscowość", "Parafia", "Imię", "Nazwisko", "Adres parafii" };
        string[] row = { "Dekanat Testowy", city, "Parafia Rzymskokatolicka świętego Testa", "Jan", $"Import{city}", "ul. Testowa 1" };
        for (var c = 0; c < header.Length; c++) { ws.Cell(1, c + 1).Value = header[c]; ws.Cell(2, c + 1).Value = row[c]; }
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        var part = new ByteArrayContent(ms.ToArray());
        part.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        return new MultipartFormDataContent { { part, "file", "proboszczowie.xlsx" } };
    }

    [Fact]
    public async Task AnAdministrator_PreviewsThenImports_ACatechistCannot()
    {
        var admin = await CreateAuthenticatedClientAsync($"admin-{Guid.NewGuid():N}@example.org", "Sekret123!", "Administrator");
        var catechist = await CreateAuthenticatedClientAsync($"kat-{Guid.NewGuid():N}@example.org", "Sekret123!", "Katechista");
        var city = $"Testowo{Guid.NewGuid():N}"[..14];

        Assert.Equal(HttpStatusCode.Forbidden, (await catechist.PostAsync("/api/parishes/import/preview", Workbook(city))).StatusCode);

        var preview = await (await admin.PostAsync("/api/parishes/import/preview", Workbook(city))).Content.ReadFromJsonAsync<ParishImportResultDto>();
        Assert.Equal((false, 1, 1), (preview!.Applied, preview.NewParishes, preview.NewPeople));
        Assert.DoesNotContain((await admin.GetFromJsonAsync<List<ParishDto>>("/api/parishes"))!, p => p.City == city);

        var imported = await (await admin.PostAsync("/api/parishes/import", Workbook(city))).Content.ReadFromJsonAsync<ParishImportResultDto>();
        Assert.True(imported!.Applied);
        var parish = (await admin.GetFromJsonAsync<List<ParishDto>>("/api/parishes"))!.Single(p => p.City == city);
        Assert.Equal(("św. Testa", "Testowy", "ul. Testowa 1", $"Jan Import{city}"), (parish.Name, parish.Deanery, parish.Address, parish.PastorFullName));

        var broken = new MultipartFormDataContent { { new ByteArrayContent(new byte[] { 1, 2 }), "file", "x.xlsx" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsync("/api/parishes/import/preview", broken)).StatusCode);
    }
}
