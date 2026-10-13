using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using ClosedXML.Excel;
using DokPortal.Application.Budget;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Budżet jako ewidencja: poprawianie wpisów, okres (rok/miesiąc) z saldem otwarcia, eksport do Excela i załączniki (faktury, paragony).</summary>
public class BudgetLedgerTests : IntegrationTestBase, IClassFixture<FakeStorageFactory>
{
    public BudgetLedgerTests(CustomWebApplicationFactory unused, FakeStorageFactory factory) : base(factory)
    {
    }

    private Task<HttpClient> UserAsync(string role) =>
        CreateAuthenticatedClientAsync($"{role.ToLower()}-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

    /// <summary>Każdy test ma własny „fundusz” w praktyce – rozróżniamy wpisy po unikalnej kategorii.</summary>
    private static async Task<BudgetEntryDto> AddAsync(HttpClient client, string fund, string date, string type, decimal amount, string category, string description = "Opis")
    {
        var response = await client.PostAsJsonAsync("/api/budget", new { Fund = fund, EntryDate = date, Description = description, Category = category, Type = type, Amount = amount });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BudgetEntryDto>(EnumJsonOptions))!;
    }

    [Fact]
    public async Task AnEntry_CanBeCorrected_ByWhoeverManagesItsFund()
    {
        var dok = await UserAsync("DyrektorDOK");
        var sksp = await UserAsync("DyrektorSKSP");
        var entry = await AddAsync(dok, "DOK", "2026-09-18", "Expense", 780m, "Materiały");
        var body = new { EntryDate = "2026-09-19", Description = "Materiały – poprawione", Category = "Książki", Type = "Expense", Amount = 870m };

        var foreign = await sksp.PutAsJsonAsync($"/api/budget/{entry.Id}", body);
        var update = await dok.PutAsJsonAsync($"/api/budget/{entry.Id}", body);
        var updated = await update.Content.ReadFromJsonAsync<BudgetEntryDto>(EnumJsonOptions);
        var missing = await dok.PutAsJsonAsync($"/api/budget/{Guid.NewGuid()}", body);

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal(870m, updated!.Amount);
        Assert.Equal(new DateOnly(2026, 9, 19), updated.EntryDate);
        Assert.Equal("Książki", updated.Category);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task EntriesNeedADescriptionAndAPositiveAmount()
    {
        var admin = await UserAsync("Administrator");

        var zero = await admin.PostAsJsonAsync("/api/budget", new { Fund = "DOK", EntryDate = "2026-09-18", Description = "x", Category = "x", Type = "Expense", Amount = 0m });
        var empty = await admin.PostAsJsonAsync("/api/budget", new { Fund = "DOK", EntryDate = "2026-09-18", Description = " ", Category = "x", Type = "Expense", Amount = 5m });

        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);
        Assert.Contains("większa od zera", await zero.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task APeriod_ShowsItsEntries_AndTheSummaryCarriesTheOpeningBalance()
    {
        // Osobna „przestrzeń” dat (rok 2031), żeby inne testy nie zmieniały sum.
        var admin = await UserAsync("Administrator");
        await AddAsync(admin, "SKSP", "2031-01-15", "Income", 1000m, "Dotacja");
        await AddAsync(admin, "SKSP", "2031-01-20", "Expense", 300m, "Materiały");
        var march = await AddAsync(admin, "SKSP", "2031-03-02", "Expense", 200m, "Materiały");
        await AddAsync(admin, "SKSP", "2031-03-10", "Income", 50m, "Ofiara");
        await AddAsync(admin, "SKSP", "2031-05-01", "Expense", 999m, "Później");

        var entries = await admin.GetFromJsonAsync<List<BudgetEntryDto>>("/api/budget?fund=SKSP&from=2031-03-01&to=2031-03-31", EnumJsonOptions);
        var summary = await admin.GetFromJsonAsync<BudgetSummaryDto>("/api/budget/summary?fund=SKSP&from=2031-03-01&to=2031-03-31");

        Assert.Equal(2, entries!.Count);
        Assert.Equal(march.Id, entries[1].Id);
        Assert.Equal(700m, summary!.OpeningBalance);
        Assert.Equal(50m, summary.Income);
        Assert.Equal(200m, summary.Expense);
        Assert.Equal(550m, summary.ClosingBalance);
        Assert.Equal(HttpStatusCode.Forbidden, (await (await UserAsync("DyrektorDOK")).GetAsync("/api/budget/summary?fund=SKSP")).StatusCode);
    }

    [Fact]
    public async Task ThePeriod_CanBeExportedToExcel_ForThoseWhoSeeTheFund()
    {
        var admin = await UserAsync("Administrator");
        await AddAsync(admin, "DOK", "2032-02-03", "Income", 120.5m, "Dotacja", "Ofiara parafii");
        await AddAsync(admin, "DOK", "2032-02-10", "Expense", 20m, "Materiały", "Zeszyty");

        var response = await admin.GetAsync("/api/budget/export?fund=DOK&from=2032-02-01&to=2032-02-28");
        var forbidden = await (await UserAsync("DyrektorSKSP")).GetAsync("/api/budget/export?fund=DOK");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", response.Content.Headers.ContentType!.MediaType);
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheet = workbook.Worksheets.First();
        Assert.Equal("Data", sheet.Cell(1, 1).GetString());
        Assert.Equal("Ofiara parafii", sheet.Cell(2, 2).GetString());
        Assert.Equal("Przychód", sheet.Cell(2, 4).GetString());
        Assert.Equal(120.5, sheet.Cell(2, 5).GetDouble());
        Assert.Equal("Wydatek", sheet.Cell(3, 4).GetString());
        Assert.Contains("Saldo", string.Join("|", sheet.Column(1).CellsUsed().Select(c => c.GetString())));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
    }

    [Fact]
    public async Task AnInvoice_CanBeAttachedToAnEntry_DownloadedAndRemoved()
    {
        var dok = await UserAsync("DyrektorDOK");
        var sksp = await UserAsync("DyrektorSKSP");
        var entry = await AddAsync(dok, "DOK", "2026-09-18", "Expense", 80m, "Materiały");
        var file = new MultipartFormDataContent();
        var part = new ByteArrayContent(Encoding.UTF8.GetBytes("FAKTURA"));
        part.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        file.Add(part, "file", "faktura.pdf");

        var foreign = await sksp.PostAsync($"/api/budget/{entry.Id}/attachments", file);
        var upload = await dok.PostAsync($"/api/budget/{entry.Id}/attachments", file);
        var attachment = await upload.Content.ReadFromJsonAsync<DokPortal.Application.Attachments.AttachmentDto>();
        var listed = (await dok.GetFromJsonAsync<List<BudgetEntryDto>>("/api/budget?fund=DOK", EnumJsonOptions))!.Single(e => e.Id == entry.Id);
        var download = await dok.GetAsync($"/api/budget/{entry.Id}/attachments/{attachment!.Id}/download");
        var delete = await dok.DeleteAsync($"/api/budget/{entry.Id}/attachments/{attachment.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        Assert.Equal("faktura.pdf", Assert.Single(listed.Attachments).FileName);
        Assert.Equal("FAKTURA", await download.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }
}
