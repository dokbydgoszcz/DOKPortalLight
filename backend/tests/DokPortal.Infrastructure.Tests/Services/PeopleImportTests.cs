using DokPortal.Application.People;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Import wklejonej listy osób: absolwenci z funkcją Katechista, uczniowie jako kandydaci SKŚP.</summary>
public class PeopleImportTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static Person NewPerson(string first, string last, string? email = null) => new()
    {
        Id = Guid.NewGuid(), FirstName = first, LastName = last, Email = email, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
    };

    private const string Graduates = """
        Szkoła Katechistów
        2023-2025 zakończyli

        Nazwisko i imię

        1 Ambrozik Joanna
        2 Burzych Magdalena
        17 Łuczak Szymon
        18 Bzdawka-Piątkowska Kamilla
        19 KOWALSKI JAN
        """;

    [Fact]
    public async Task APastedListOfGraduates_CreatesTheMissingPeople_AndGrantsTheCatechistFunctionOnce()
    {
        await using var db = CreateContext();
        var me = NewPerson("Szymon", "Łuczak", "szymon@example.org");
        var catechist = NewPerson("Joanna", "Ambrozik");
        db.People.AddRange(me, catechist);
        db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = catechist.Id, Type = FunctionType.Catechist });
        await db.SaveChangesAsync();
        var request = new PeopleImportRequest { Text = Graduates, Function = FunctionType.Catechist, FunctionNote = "Absolwent SKŚP 2023–2025" };

        var preview = await new PeopleImportService(db).RunAsync(request, apply: false, default);

        Assert.Equal((false, 3, 2, 4), (preview.Applied, preview.NewPeople, preview.ExistingPeople, preview.FunctionsGranted));
        Assert.Equal(new[] { "Szkoła Katechistów", "2023-2025 zakończyli", "Nazwisko i imię" }, preview.SkippedLines);
        Assert.Equal(("Kamilla", "Bzdawka-Piątkowska"), (preview.Rows[3].FirstName, preview.Rows[3].LastName));
        Assert.Equal(("Jan", "Kowalski"), (preview.Rows[4].FirstName, preview.Rows[4].LastName));
        Assert.True(preview.Rows[0].HadFunction);
        Assert.Equal(2, await db.People.CountAsync());

        await new PeopleImportService(db).RunAsync(request, apply: true, default);

        Assert.Equal(5, await db.People.CountAsync());
        var functions = await db.PersonFunctions.Where(f => f.Type == FunctionType.Catechist).ToListAsync();
        Assert.Equal(5, functions.Count);
        Assert.Equal(4, functions.Count(f => f.Notes == "Absolwent SKŚP 2023–2025"));
        Assert.Contains(functions, f => f.PersonId == me.Id);

        var again = await new PeopleImportService(db).RunAsync(request, apply: true, default);
        Assert.Equal((0, 5, 0), (again.NewPeople, again.ExistingPeople, again.FunctionsGranted));
    }

    [Fact]
    public async Task StudentsUnderYearHeadings_AreEnrolledAsCandidatesOfThatYear()
    {
        await using var db = CreateContext();
        var already = NewPerson("Emilia", "Glugla");
        db.People.Add(already);
        db.Candidates.Add(new Candidate { Id = Guid.NewGuid(), PersonId = already.Id, FormationYear = 3, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var request = new PeopleImportRequest
        {
            EnrollAsCandidates = true,
            Text = """
                Szkoła Katechistów rok akademicki 2026/2027
                Nazwisko i imię
                Rok III
                1 Baranowska Małgorzata
                3 Glugla Emilia
                Rok II
                1 Augustynowicz Anna	anna@example.org	600 100 200
                """
        };

        var result = await new PeopleImportService(db).RunAsync(request, apply: true, default);

        Assert.Equal(2, result.CandidatesEnrolled);
        Assert.Equal(new int?[] { 3, null, 2 }, result.Rows.Select(r => r.CandidateYear));
        Assert.Contains(result.Rows[1].Warnings, w => w.Contains("Jest już kandydatem SKŚP (rok 3)"));
        var anna = await db.People.SingleAsync(p => p.LastName == "Augustynowicz");
        Assert.Equal(("anna@example.org", "600 100 200"), (anna.Email, anna.Phone));
        var candidate = await db.Candidates.Include(c => c.Events).SingleAsync(c => c.PersonId == anna.Id);
        Assert.Equal(2, candidate.FormationYear);
        Assert.Equal(CandidateFormationEventKind.Enrolled, candidate.Events.Single().Kind);
        Assert.Empty(await db.PersonFunctions.ToListAsync());
    }

    [Fact]
    public async Task UnclearInput_IsExplained()
    {
        await using var db = CreateContext();
        var other = NewPerson("Ktoś", "Inny", "zajety@example.org");
        var twin1 = NewPerson("Jan", "Nowak", "jan@example.org");
        twin1.CreatedAtUtc = DateTime.UtcNow.AddYears(-1);
        var twin2 = NewPerson("Jan", "Nowak");
        db.People.AddRange(other, twin1, twin2);
        await db.SaveChangesAsync();
        var service = new PeopleImportService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RunAsync(new PeopleImportRequest { Text = "Nazwisko i imię\n\n" }, false, default));
        var noYear = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.RunAsync(new PeopleImportRequest { Text = "Kot Ala", EnrollAsCandidates = true }, false, default));
        Assert.Contains("rok formacji", noYear.Message);

        var result = await service.RunAsync(new PeopleImportRequest
        {
            FirstNameFirst = true,
            Text = "Jan Nowak; inny@example.org\nAla Kot; zajety@example.org\nMaria Anna Wiśniewska\nAla Kot\n1;Lis;Ewa"
        }, false, default);

        Assert.Contains(result.Rows[0].Warnings, w => w.Contains("2 osób"));
        Assert.Contains(result.Rows[0].Warnings, w => w.Contains("ma już inny e-mail"));
        Assert.Contains(result.Rows[1].Warnings, w => w.Contains("zajety@example.org"));
        Assert.Equal(("Maria", "Anna Wiśniewska"), (result.Rows[2].FirstName, result.Rows[2].LastName));
        Assert.Contains(result.Rows[2].Warnings, w => w.Contains("dwa słowa"));
        Assert.Contains(result.Rows[3].Warnings, w => w.Contains("drugi raz"));
        Assert.Equal(("Lis", "Ewa"), (result.Rows[4].FirstName, result.Rows[4].LastName));
    }
}
