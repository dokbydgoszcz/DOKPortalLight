using ClosedXML.Excel;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Import spisu parafii z proboszczami (arkusz ze strony diecezji).</summary>
public class ParishImportTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static readonly string[] Header =
        { "Dekanat", "Miejscowość", "Parafia", "Funkcja", "Tytuły", "Imię", "Nazwisko", "Zgromadzenie", "Proboszcz od", "Tel. proboszcza", "E-mail proboszcza", "Adres parafii", "Tel. parafii", "E-mail parafii", "WWW" };

    private static MemoryStream Sheet(params object?[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Proboszczowie");
        for (var c = 0; c < Header.Length; c++) ws.Cell(1, c + 1).Value = Header[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 2, c + 1).Value = rows[r][c] switch { null => Blank.Value, int i => i, var o => o.ToString() };
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    private static object?[] Row(string city, string parish, string first, string last, int? since = null, string? mobile = null, string? mail = null,
        string deanery = "Dekanat Bydgoszcz V – Fordon", string function = "proboszcz", string? titles = null, string? order = null) =>
        new object?[] { deanery, city, parish, function, titles, first, last, order, since, mobile, mail, "ul. Kościelna 1, 85-000 Bydgoszcz", "52 000 00 00", "parafia@example.org", "www.parafia.example.org" };

    private static Person NewPerson(string first, string last, string? email = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, Email = email, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task ThePreviewChangesNothing_AndTheImportMatchesCreatesAndAssigns()
    {
        await using var db = CreateContext();
        var mateusz = new Parish { Id = Guid.NewGuid(), Name = "św. Mateusza", City = "Bydgoszcz" };
        var marek = new Parish { Id = Guid.NewGuid(), Name = "Parafia św. Marka w Bydgoszczy" };
        db.Parishes.AddRange(mateusz, marek);
        var known = NewPerson("Jan", "Nowak", "jan.nowak@example.org");
        var oldPastor = NewPerson("Stary", "Proboszcz");
        var taken = NewPerson("Ktoś", "Inny", "zajety@example.org");
        db.People.AddRange(known, oldPastor, taken);
        db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = oldPastor.Id, Type = FunctionType.Pastor, ParishId = marek.Id });
        await db.SaveChangesAsync();
        object?[][] rows =
        {
            Row("Bydgoszcz", "Parafia Rzymskokatolicka świętego Mateusza Apostoła i Ewangelisty", "Jan", "Nowak", 2024, "600 100 200", "inny@example.org"),
            Row("Bydgoszcz", "Parafia Rzymskokatolicka świętego Marka", "Adam", "Lis", titles: "dr", order: "MSF"),
            Row("Wysoka", "Parafia Rzymskokatolicka Najświętszej Maryi Panny Różańcowej", "Adam", "Lis", function: "administrator parafii"),
            Row("Bądecz", "Parafia Rzymskokatolicka świętego Józefa", "Piotr", "Kot", mail: "zajety@example.org", deanery: "Dekanat Wysoka"),
        };

        var preview = await new ParishImportService(db).RunAsync(Sheet(rows), apply: false, default);

        Assert.False(preview.Applied);
        Assert.Equal((2, 2, 2, 1, 3, 0, 1), (preview.NewParishes, preview.MatchedParishes, preview.NewPeople, preview.ExistingPeople, preview.PastorsSet, preview.PastorsUnchanged, preview.PastorsReplaced));
        Assert.Equal("św. Mateusza", preview.Rows[0].ParishName);
        Assert.Equal("Stary Proboszcz", preview.Rows[1].PreviousPastor);
        Assert.Equal("NMP Różańcowej", preview.Rows[2].ParishName);
        Assert.Equal(2, await db.Parishes.CountAsync());
        Assert.Equal(3, await db.People.CountAsync());

        var result = await new ParishImportService(db).RunAsync(Sheet(rows), apply: true, default);

        Assert.True(result.Applied);
        Assert.Contains(result.Rows[3].Warnings, w => w.Contains("zajety@example.org"));
        Assert.Equal(4, await db.Parishes.CountAsync());
        var wysoka = await db.Parishes.SingleAsync(p => p.City == "Wysoka");
        Assert.Equal(("NMP Różańcowej", "Bydgoszcz V – Fordon", "52 000 00 00", "www.parafia.example.org"), (wysoka.Name, wysoka.Deanery, wysoka.Phone, wysoka.Website));
        Assert.Equal("Wysoka", (await db.Parishes.SingleAsync(p => p.Id == wysoka.Id)).City);
        var lis = await db.People.Include(p => p.Functions).SingleAsync(p => p.LastName == "Lis");
        Assert.Equal(new[] { marek.Id, wysoka.Id }.OrderBy(x => x), lis.Functions.Where(f => f.Type == FunctionType.Pastor).Select(f => f.ParishId!.Value).OrderBy(x => x));
        Assert.Contains("dr", lis.Notes);
        Assert.Contains("MSF", lis.Notes);
        Assert.Contains(lis.Functions, f => f.Notes == "administrator parafii");
        var nowak = await db.People.Include(p => p.Functions).SingleAsync(p => p.Id == known.Id);
        Assert.Equal("jan.nowak@example.org", nowak.Email);
        Assert.Equal("600 100 200", nowak.Phone);
        Assert.Equal("proboszcz od 2024", nowak.Functions.Single().Notes);
        Assert.Null((await db.PersonFunctions.SingleAsync(f => f.PersonId == oldPastor.Id)).ParishId);
        Assert.Null((await db.People.SingleAsync(p => p.LastName == "Kot")).Email);

        var again = await new ParishImportService(db).RunAsync(Sheet(rows), apply: true, default);
        Assert.Equal((0, 4, 0, 4), (again.NewParishes, again.MatchedParishes, again.NewPeople, again.PastorsUnchanged));
    }

    [Fact]
    public async Task AWrongFileOrMissingColumns_AreExplained()
    {
        await using var db = CreateContext();
        var service = new ParishImportService(db);

        var notExcel = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync(new MemoryStream(new byte[] { 1, 2, 3 }), false, default));
        Assert.Contains("Excel", notExcel.Message);

        using var wb = new XLWorkbook();
        wb.AddWorksheet("A").Cell(1, 1).Value = "Parafia";
        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        var noColumns = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync(ms, false, default));
        Assert.Contains("Miejscowość, Parafia, Imię, Nazwisko", noColumns.Message);

        var empty = await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync(Sheet(), false, default));
        Assert.Contains("żadnej parafii", empty.Message);
    }

    [Fact]
    public async Task SeveralPeopleWithTheSameName_PreferThePastor_AndAreFlagged()
    {
        await using var db = CreateContext();
        var a = NewPerson("Jan", "Kowalski");
        var b = NewPerson("Jan", "Kowalski");
        db.People.AddRange(a, b);
        db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = b.Id, Type = FunctionType.Pastor });
        db.Parishes.AddRange(new Parish { Id = Guid.NewGuid(), Name = "św. Józefa", City = "Bądecz" }, new Parish { Id = Guid.NewGuid(), Name = "św. Józefa Robotnika", City = "Bądecz" });
        await db.SaveChangesAsync();

        var result = await new ParishImportService(db).RunAsync(Sheet(Row("Bądecz", "Parafia pw. św. Józefa", "Jan", "Kowalski")), true, default);

        Assert.Contains(result.Rows[0].Warnings, w => w.Contains("2 osób"));
        Assert.Equal("św. Józefa", result.Rows[0].ParishName);
        Assert.Equal(b.Id, (await db.PersonFunctions.SingleAsync()).PersonId);
    }

    [Theory]
    [InlineData("Parafia Rzymskokatolicka świętego Józefa", "św. Józefa")]
    [InlineData("Parafia Rzymskokatolicka Świętego Krzyża", "Świętego Krzyża")]
    [InlineData("Parafia Rzymskokatolicka Świętej Trójcy", "Świętej Trójcy")]
    [InlineData("Parafia pw. Wniebowzięcia NMP", "Wniebowzięcia NMP")]
    [InlineData("Parafia Rzymskokatolicka błogosławionego Michała Kozala", "bł. Michała Kozala")]
    public void ParishNames_AreShortenedLikeInTheRegister(string full, string expected) =>
        Assert.Equal(expected, ParishImportService.ShortName(full));
}
