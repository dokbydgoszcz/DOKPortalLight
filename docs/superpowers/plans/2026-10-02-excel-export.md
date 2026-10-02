# Eksport list do Excela — plan implementacji

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Uprawnieni użytkownicy pobierają z każdego z ośmiu ekranów list plik `.xlsx` z wszystkimi nieusuniętymi rekordami tej listy.

**Architecture:** Serwerowy `ExportService` (ClosedXML) buduje skoroszyt z jednego prywatnego helpera `BuildWorkbook`; `ExportController` wystawia osiem endpointów `GET /api/export/*` z rolami jak przy edycji listy i wpisem w audycie. Frontend: `ExportService` (pobranie bloba + zapis pliku) i wspólny `ExportButtonComponent` konfigurowany kluczem listy.

**Tech Stack:** ASP.NET Core 8, EF Core, ClosedXML (MIT), xUnit; Angular (standalone, signals), Vitest.

**Spec:** `docs/superpowers/specs/2026-10-02-excel-export-design.md`

## Global Constraints

- Format: `.xlsx`, biblioteka `ClosedXML`, generowanie po stronie serwera.
- Eksport = wszystkie nieusunięte rekordy listy, bez filtrów z ekranu (globalny filtr soft-delete działa automatycznie).
- Brak migracji bazy.
- Nagłówki kolumn po polsku; daty `yyyy-MM-dd`; puste wartości = pusta komórka; Tak/Nie dla wartości logicznych.
- Pole `Person.Notes` NIE jest eksportowane.
- Każdy eksport zapisuje wpis audytu: akcja `ExportData`, opis = nazwa listy (np. `people`), wynik `AuditResult.Allowed`.
- Role (osobno na każdym endpoincie): people = Administrator, DyrektorSKSP, DyrektorDOK; dok-cases = Administrator, DyrektorDOK; candidates, missions, formators = Administrator, DyrektorSKSP; supervisions = Administrator, DyrektorDOK, DyrektorSKSP, Superwizor; meetings = Administrator, DyrektorDOK; parishes = Administrator.
- Commity i push bezpośrednio na `master`, bez PR. Nie commitować `backend/src/DokPortal.Api/appsettings.Development.json` ani `.claude/` (stage'ować tylko wskazane pliki).
- Stopka commita: `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`.
- Komendy backendu uruchamiać z `C:\eu02_install\DOKPortalLight\backend`, frontendu z `...\frontend`.

---

### Task 1: Fundament eksportu + lista Osoby

**Files:**
- Modify: `backend/src/DokPortal.Infrastructure/DokPortal.Infrastructure.csproj` (pakiet ClosedXML)
- Create: `backend/src/DokPortal.Application/Export/IExportService.cs`
- Create: `backend/src/DokPortal.Infrastructure/Services/ExportService.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/ExportServiceTests.cs`

**Interfaces:**
- Produces: `IExportService.ExportPeopleAsync(CancellationToken ct) : Task<byte[]>`; w `ExportService` prywatne: `BuildWorkbook(string sheetName, string[] headers, IEnumerable<object?[]> rows) : byte[]`, `FormatDate(DateOnly?) : string?`, `YesNo(bool) : string`. Wartości komórek: `null` → pusta, `int` → liczba, reszta → `ToString()`.

- [ ] **Step 1: Dodaj pakiet**

```bash
cd C:/eu02_install/DOKPortalLight/backend/src/DokPortal.Infrastructure && dotnet add package ClosedXML
```
Expected: pakiet dodany do `.csproj` (sprawdź, że wersja to 0.10x lub nowsza). Projekt testowy dziedziczy go tranzytywnie przez `ProjectReference`.

- [ ] **Step 2: Napisz test (czerwony)**

Utwórz `ExportServiceTests.cs`:

```csharp
using ClosedXML.Excel;
using DokPortal.Domain.Entities;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

public class ExportServiceTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IXLWorksheet OpenSheet(byte[] bytes) => new XLWorkbook(new MemoryStream(bytes)).Worksheet(1);

    private static string[] Row(IXLWorksheet sheet, int row, int columns) =>
        Enumerable.Range(1, columns).Select(c => sheet.Cell(row, c).GetString()).ToArray();

    [Fact]
    public async Task ExportPeopleAsync_WritesPolishHeadersSortedRowsAndSkipsNotesAndDeleted()
    {
        await using var db = CreateContext();
        var parish = new Parish { Id = Guid.NewGuid(), Name = "Św. Pawła" };
        db.Parishes.Add(parish);
        db.People.AddRange(
            new Person
            {
                Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", Email = "jan@example.org", Phone = "600100200",
                BirthDate = new DateOnly(1990, 5, 17), ParishId = parish.Id, Notes = "tajna uwaga", NameDayMonth = 6, NameDayDay = 24
            },
            new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Adamska" },
            new Person { Id = Guid.NewGuid(), FirstName = "Usunięty", LastName = "Zenon", DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportPeopleAsync(default));

        Assert.Equal(
            new[] { "Imię", "Nazwisko", "E-mail", "Telefon", "Data urodzenia", "Parafia", "Imieniny (DD.MM)" },
            Row(sheet, 1, 7));
        Assert.Equal(new[] { "Anna", "Adamska", "", "", "", "", "" }, Row(sheet, 2, 7));
        Assert.Equal(
            new[] { "Jan", "Kowalski", "jan@example.org", "600100200", "1990-05-17", "Św. Pawła", "24.06" },
            Row(sheet, 3, 7));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
        Assert.DoesNotContain(sheet.CellsUsed(), c => c.GetString().Contains("tajna uwaga"));
    }
}
```

- [ ] **Step 3: Uruchom — ma się nie skompilować**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~ExportServiceTests" 2>&1 | tail -8
```
Expected: błąd kompilacji (`ExportService` nie istnieje).

- [ ] **Step 4: Interfejs i implementacja**

`backend/src/DokPortal.Application/Export/IExportService.cs`:

```csharp
namespace DokPortal.Application.Export;

public interface IExportService
{
    Task<byte[]> ExportPeopleAsync(CancellationToken ct);
}
```

`backend/src/DokPortal.Infrastructure/Services/ExportService.cs`:

```csharp
using ClosedXML.Excel;
using DokPortal.Application.Export;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Services;

public class ExportService : IExportService
{
    private readonly AppDbContext _db;

    public ExportService(AppDbContext db) => _db = db;

    public async Task<byte[]> ExportPeopleAsync(CancellationToken ct)
    {
        var people = await _db.People.AsNoTracking()
            .Include(p => p.Parish)
            .OrderBy(p => p.LastName).ThenBy(p => p.FirstName)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Osoby",
            new[] { "Imię", "Nazwisko", "E-mail", "Telefon", "Data urodzenia", "Parafia", "Imieniny (DD.MM)" },
            people.Select(p => new object?[]
            {
                p.FirstName, p.LastName, p.Email, p.Phone, FormatDate(p.BirthDate), p.Parish?.Name,
                p.NameDayDay is int day && p.NameDayMonth is int month ? $"{day:00}.{month:00}" : null
            }));
    }

    private static string? FormatDate(DateOnly? date) => date?.ToString("yyyy-MM-dd");

    private static string YesNo(bool value) => value ? "Tak" : "Nie";

    private static byte[] BuildWorkbook(string sheetName, string[] headers, IEnumerable<object?[]> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);

        for (var column = 0; column < headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }
        sheet.Row(1).Style.Font.Bold = true;

        var rowNumber = 2;
        foreach (var row in rows)
        {
            for (var column = 0; column < row.Length; column++)
            {
                switch (row[column])
                {
                    case null:
                        break;
                    case int number:
                        sheet.Cell(rowNumber, column + 1).Value = number;
                        break;
                    default:
                        sheet.Cell(rowNumber, column + 1).Value = row[column]!.ToString();
                        break;
                }
            }
            rowNumber++;
        }

        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
```
(`YesNo` zostanie użyte w Task 2; kompilator dopuszcza nieużywaną metodę prywatną — jeśli analizator zgłosi ostrzeżenie, zignoruj do Task 2.)

- [ ] **Step 5: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~ExportServiceTests" 2>&1 | tail -4
```
Expected: `powodzenie: 1`.

- [ ] **Step 6: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Infrastructure/DokPortal.Infrastructure.csproj backend/src/DokPortal.Application/Export/IExportService.cs backend/src/DokPortal.Infrastructure/Services/ExportService.cs backend/tests/DokPortal.Infrastructure.Tests/Services/ExportServiceTests.cs
git commit -m "$(cat <<'EOF'
Dodaj ExportService z eksportem listy Osoby do Excela

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Eksport Podopieczni DOK i Kandydaci SKŚP

**Files:**
- Modify: `backend/src/DokPortal.Application/Export/IExportService.cs`, `backend/src/DokPortal.Infrastructure/Services/ExportService.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/ExportServiceTests.cs`

**Interfaces:**
- Consumes: `BuildWorkbook`, `FormatDate`, `YesNo` z Task 1.
- Produces: `ExportDokCasesAsync(CancellationToken)`, `ExportCandidatesAsync(CancellationToken)` — oba `Task<byte[]>`; w `ExportService` statyczne słowniki `PathLabels` (`DokPath`→string) i `StageLabels` (`DokStage`→string).

- [ ] **Step 1: Dopisz testy (czerwone)** — dodaj `using DokPortal.Domain.Enums;` na górze pliku oraz dwa testy w klasie:

```csharp
    private static Person NewPerson(string first, string last) => new() { Id = Guid.NewGuid(), FirstName = first, LastName = last };

    [Fact]
    public async Task ExportDokCasesAsync_WritesPolishLabelsAndNames()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        var catechist = NewPerson("Anna", "Nowak");
        var mentor = NewPerson("Piotr", "Wiśniewski");
        db.People.AddRange(person, catechist, mentor);
        db.DokCases.AddRange(
            new DokCase
            {
                Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id,
                Path = DokPath.Confirmation, Stage = DokStage.Formation, LastMeetingDate = new DateOnly(2026, 9, 30)
            },
            new DokCase
            {
                Id = Guid.NewGuid(), PersonId = mentor.Id, CatechistPersonId = catechist.Id, MentorPersonId = person.Id,
                Path = DokPath.ReturnToUnity, Stage = DokStage.Graduate, CompletedAtUtc = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc)
            },
            new DokCase
            {
                Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id,
                Path = DokPath.Communion, Stage = DokStage.Application, DeletedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportDokCasesAsync(default));

        Assert.Equal(
            new[] { "Osoba", "Ścieżka", "Etap", "Katechista", "Opiekun (mentor)", "Data ostatniego spotkania", "Data zakończenia" },
            Row(sheet, 1, 7));
        Assert.Equal(
            new[] { "Jan Kowalski", "Bierzmowanie", "Formacja", "Anna Nowak", "", "2026-09-30", "" },
            Row(sheet, 2, 7));
        Assert.Equal(
            new[] { "Piotr Wiśniewski", "Powrót do Jedności", "Absolwent", "Anna Nowak", "Jan Kowalski", "", "2026-08-01" },
            Row(sheet, 3, 7));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task ExportCandidatesAsync_WritesNumbersAndYesNo()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        db.People.Add(person);
        db.Candidates.AddRange(
            new Candidate
            {
                Id = Guid.NewGuid(), PersonId = person.Id, Year = 2, AttendancePercentage = 85,
                OpinionsCollected = 1, OpinionsRequired = 2, IsRetreatCompleted = true
            },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, Year = 3, OpinionsCollected = 0, OpinionsRequired = 2 },
            new Candidate { Id = Guid.NewGuid(), PersonId = person.Id, Year = 1, DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportCandidatesAsync(default));

        Assert.Equal(
            new[] { "Osoba", "Rok", "Frekwencja (%)", "Opinie zebrane", "Opinie wymagane", "Rekolekcje" },
            Row(sheet, 1, 6));
        Assert.Equal(new[] { "Jan Kowalski", "2", "85", "1", "2", "Tak" }, Row(sheet, 2, 6));
        Assert.Equal(new[] { "Jan Kowalski", "3", "", "0", "2", "Nie" }, Row(sheet, 3, 6));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
    }
```

- [ ] **Step 2: Uruchom — ma się nie skompilować** (`ExportDokCasesAsync`/`ExportCandidatesAsync` nie istnieją).

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~ExportServiceTests" 2>&1 | tail -6
```

- [ ] **Step 3: Implementacja** — w `IExportService` dodaj:

```csharp
    Task<byte[]> ExportDokCasesAsync(CancellationToken ct);
    Task<byte[]> ExportCandidatesAsync(CancellationToken ct);
```

W `ExportService.cs` dodaj `using DokPortal.Domain.Enums;`, a w klasie (nad `FormatDate`):

```csharp
    private static readonly Dictionary<DokPath, string> PathLabels = new()
    {
        [DokPath.BaptismCandidate] = "Kandydaci do Chrztu",
        [DokPath.Confirmation] = "Bierzmowanie",
        [DokPath.Communion] = "Stół Pański",
        [DokPath.Conversion] = "Konwersja",
        [DokPath.ReturnToUnity] = "Powrót do Jedności"
    };

    private static readonly Dictionary<DokStage, string> StageLabels = new()
    {
        [DokStage.Application] = "Zgłoszenie",
        [DokStage.Formation] = "Formacja",
        [DokStage.Sacrament] = "Sakrament",
        [DokStage.Graduate] = "Absolwent"
    };

    public async Task<byte[]> ExportDokCasesAsync(CancellationToken ct)
    {
        var cases = await _db.DokCases.AsNoTracking()
            .Include(c => c.Person).Include(c => c.CatechistPerson).Include(c => c.MentorPerson)
            .OrderBy(c => c.Person!.LastName).ThenBy(c => c.Person!.FirstName)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Podopieczni DOK",
            new[] { "Osoba", "Ścieżka", "Etap", "Katechista", "Opiekun (mentor)", "Data ostatniego spotkania", "Data zakończenia" },
            cases.Select(c => new object?[]
            {
                c.Person?.FullName, PathLabels[c.Path], StageLabels[c.Stage], c.CatechistPerson?.FullName,
                c.MentorPerson?.FullName, FormatDate(c.LastMeetingDate), c.CompletedAtUtc?.ToString("yyyy-MM-dd")
            }));
    }

    public async Task<byte[]> ExportCandidatesAsync(CancellationToken ct)
    {
        var candidates = await _db.Candidates.AsNoTracking()
            .Include(c => c.Person)
            .OrderBy(c => c.Person!.LastName).ThenBy(c => c.Person!.FirstName).ThenBy(c => c.Year)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Kandydaci SKŚP",
            new[] { "Osoba", "Rok", "Frekwencja (%)", "Opinie zebrane", "Opinie wymagane", "Rekolekcje" },
            candidates.Select(c => new object?[]
            {
                c.Person?.FullName, c.Year, c.AttendancePercentage, c.OpinionsCollected, c.OpinionsRequired, YesNo(c.IsRetreatCompleted)
            }));
    }
```
Uwaga: `int?` z wartością jest pudełkowane jako `int` (pasuje do `case int`), a `null` daje pustą komórkę.

- [ ] **Step 4: Uruchom — zielone** (`powodzenie: 3`).

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~ExportServiceTests" 2>&1 | tail -4
```

- [ ] **Step 5: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Application/Export/IExportService.cs backend/src/DokPortal.Infrastructure/Services/ExportService.cs backend/tests/DokPortal.Infrastructure.Tests/Services/ExportServiceTests.cs
git commit -m "$(cat <<'EOF'
Eksport do Excela: Podopieczni DOK i Kandydaci SKŚP

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Eksport Misje, Formatorzy, Superwizje

**Files:**
- Modify: `backend/src/DokPortal.Application/Export/IExportService.cs`, `backend/src/DokPortal.Infrastructure/Services/ExportService.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/ExportServiceTests.cs`

**Interfaces:**
- Consumes: `BuildWorkbook`, `FormatDate` z Task 1.
- Produces: `ExportMissionsAsync`, `ExportFormatorsAsync`, `ExportSupervisionsAsync` (`Task<byte[]>`, każdy z `CancellationToken ct`); `InstitutionLabels` (`Institution`→"SKŚP"/"DOK").

- [ ] **Step 1: Dopisz testy (czerwone)**

```csharp
    [Fact]
    public async Task ExportMissionsAsync_WritesDatesAndPlaces()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        db.People.Add(person);
        db.CanonicalMissions.AddRange(
            new CanonicalMission
            {
                Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "Parafia św. Jana",
                MissionStartDate = new DateOnly(2026, 1, 1), MissionEndDate = new DateOnly(2027, 1, 1),
                GrantedDate = new DateOnly(2025, 12, 20), GrantedPlace = "Bydgoszcz", SupervisionGroup = "Grupa A"
            },
            new CanonicalMission
            {
                Id = Guid.NewGuid(), PersonId = person.Id, ServicePlace = "Usunięta",
                MissionStartDate = new DateOnly(2020, 1, 1), MissionEndDate = new DateOnly(2021, 1, 1), DeletedAtUtc = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportMissionsAsync(default));

        Assert.Equal(
            new[] { "Katechista", "Miejsce posługi", "Data od", "Data do", "Data udzielenia", "Miejsce udzielenia", "Grupa superwizyjna" },
            Row(sheet, 1, 7));
        Assert.Equal(
            new[] { "Jan Kowalski", "Parafia św. Jana", "2026-01-01", "2027-01-01", "2025-12-20", "Bydgoszcz", "Grupa A" },
            Row(sheet, 2, 7));
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task ExportFormatorsAsync_WritesPersonAndFunction()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        db.People.Add(person);
        db.Formators.AddRange(
            new Formator { Id = Guid.NewGuid(), PersonId = person.Id, Function = "Wykładowca" },
            new Formator { Id = Guid.NewGuid(), PersonId = person.Id, Function = "Usunięty", DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportFormatorsAsync(default));

        Assert.Equal(new[] { "Osoba", "Funkcja" }, Row(sheet, 1, 2));
        Assert.Equal(new[] { "Jan Kowalski", "Wykładowca" }, Row(sheet, 2, 2));
        Assert.Equal(2, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task ExportSupervisionsAsync_WritesInstitutionLabelAndCounts()
    {
        await using var db = CreateContext();
        db.Supervisions.AddRange(
            new Supervision
            {
                Id = Guid.NewGuid(), Institution = Institution.SKSP, GroupLabel = "Grupa A", SupervisionDate = new DateOnly(2026, 9, 15),
                AttendeesCount = 8, ExpectedCount = 10, Topic = "Modlitwa", Conclusion = "Wnioski"
            },
            new Supervision { Id = Guid.NewGuid(), Institution = Institution.DOK, GroupLabel = "Grupa B", SupervisionDate = new DateOnly(2026, 9, 20) });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportSupervisionsAsync(default));

        Assert.Equal(new[] { "Instytucja", "Grupa", "Data", "Obecnych", "Oczekiwanych", "Temat", "Wnioski" }, Row(sheet, 1, 7));
        Assert.Equal(new[] { "SKŚP", "Grupa A", "2026-09-15", "8", "10", "Modlitwa", "Wnioski" }, Row(sheet, 2, 7));
        Assert.Equal(new[] { "DOK", "Grupa B", "2026-09-20", "", "", "", "" }, Row(sheet, 3, 7));
    }
```

- [ ] **Step 2: Uruchom — ma się nie skompilować.**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Infrastructure.Tests --filter "FullyQualifiedName~ExportServiceTests" 2>&1 | tail -6
```

- [ ] **Step 3: Implementacja** — w `IExportService` dodaj:

```csharp
    Task<byte[]> ExportMissionsAsync(CancellationToken ct);
    Task<byte[]> ExportFormatorsAsync(CancellationToken ct);
    Task<byte[]> ExportSupervisionsAsync(CancellationToken ct);
```

W `ExportService` dodaj:

```csharp
    private static readonly Dictionary<Institution, string> InstitutionLabels = new()
    {
        [Institution.SKSP] = "SKŚP",
        [Institution.DOK] = "DOK"
    };

    public async Task<byte[]> ExportMissionsAsync(CancellationToken ct)
    {
        var missions = await _db.CanonicalMissions.AsNoTracking()
            .Include(m => m.Person)
            .OrderBy(m => m.Person!.LastName).ThenBy(m => m.Person!.FirstName).ThenBy(m => m.MissionStartDate)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Katechiści posłani",
            new[] { "Katechista", "Miejsce posługi", "Data od", "Data do", "Data udzielenia", "Miejsce udzielenia", "Grupa superwizyjna" },
            missions.Select(m => new object?[]
            {
                m.Person?.FullName, m.ServicePlace, FormatDate(m.MissionStartDate), FormatDate(m.MissionEndDate),
                FormatDate(m.GrantedDate), m.GrantedPlace, m.SupervisionGroup
            }));
    }

    public async Task<byte[]> ExportFormatorsAsync(CancellationToken ct)
    {
        var formators = await _db.Formators.AsNoTracking()
            .Include(f => f.Person)
            .OrderBy(f => f.Person!.LastName).ThenBy(f => f.Person!.FirstName)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Formatorzy SKŚP",
            new[] { "Osoba", "Funkcja" },
            formators.Select(f => new object?[] { f.Person?.FullName, f.Function }));
    }

    public async Task<byte[]> ExportSupervisionsAsync(CancellationToken ct)
    {
        var supervisions = await _db.Supervisions.AsNoTracking()
            .OrderBy(s => s.SupervisionDate).ThenBy(s => s.GroupLabel)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Superwizje",
            new[] { "Instytucja", "Grupa", "Data", "Obecnych", "Oczekiwanych", "Temat", "Wnioski" },
            supervisions.Select(s => new object?[]
            {
                InstitutionLabels[s.Institution], s.GroupLabel, FormatDate(s.SupervisionDate),
                s.AttendeesCount, s.ExpectedCount, s.Topic, s.Conclusion
            }));
    }
```

- [ ] **Step 4: Uruchom — zielone** (`powodzenie: 6`).

- [ ] **Step 5: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Application/Export/IExportService.cs backend/src/DokPortal.Infrastructure/Services/ExportService.cs backend/tests/DokPortal.Infrastructure.Tests/Services/ExportServiceTests.cs
git commit -m "$(cat <<'EOF'
Eksport do Excela: Misje, Formatorzy, Superwizje

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Eksport Spotkania i Parafie

**Files:**
- Modify: `backend/src/DokPortal.Application/Export/IExportService.cs`, `backend/src/DokPortal.Infrastructure/Services/ExportService.cs`
- Test: `backend/tests/DokPortal.Infrastructure.Tests/Services/ExportServiceTests.cs`

**Interfaces:**
- Consumes: `BuildWorkbook`, `FormatDate`, `YesNo` z Task 1.
- Produces: `ExportMeetingsAsync`, `ExportParishesAsync` (`Task<byte[]>`, każdy z `CancellationToken ct`).

- [ ] **Step 1: Dopisz testy (czerwone)**

```csharp
    [Fact]
    public async Task ExportMeetingsAsync_UsesCasePersonOrGroupLabelAndAttendanceText()
    {
        await using var db = CreateContext();
        var person = NewPerson("Jan", "Kowalski");
        var catechist = NewPerson("Anna", "Nowak");
        db.People.AddRange(person, catechist);
        var dokCase = new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id,
            Path = DokPath.Confirmation, Stage = DokStage.Formation
        };
        db.DokCases.Add(dokCase);
        db.Meetings.AddRange(
            new Meeting { Id = Guid.NewGuid(), DokCaseId = dokCase.Id, MeetingDate = new DateOnly(2026, 10, 1), IsAttended = true, Notes = "ok" },
            new Meeting { Id = Guid.NewGuid(), GroupLabel = "Grupa B", MeetingDate = new DateOnly(2026, 10, 2) },
            new Meeting { Id = Guid.NewGuid(), GroupLabel = "Grupa C", MeetingDate = new DateOnly(2026, 10, 3), IsAttended = false },
            new Meeting { Id = Guid.NewGuid(), GroupLabel = "Usunięte", MeetingDate = new DateOnly(2026, 10, 4), DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportMeetingsAsync(default));

        Assert.Equal(new[] { "Data", "Podopieczny / grupa", "Obecność", "Uwagi" }, Row(sheet, 1, 4));
        Assert.Equal(new[] { "2026-10-01", "Jan Kowalski", "Tak", "ok" }, Row(sheet, 2, 4));
        Assert.Equal(new[] { "2026-10-02", "Grupa B", "—", "" }, Row(sheet, 3, 4));
        Assert.Equal(new[] { "2026-10-03", "Grupa C", "Nie", "" }, Row(sheet, 4, 4));
        Assert.Equal(4, sheet.LastRowUsed()!.RowNumber());
    }

    [Fact]
    public async Task ExportParishesAsync_WritesNameAndCitySortedByName()
    {
        await using var db = CreateContext();
        db.Parishes.AddRange(
            new Parish { Id = Guid.NewGuid(), Name = "Św. Pawła", City = "Bydgoszcz" },
            new Parish { Id = Guid.NewGuid(), Name = "Matki Bożej" },
            new Parish { Id = Guid.NewGuid(), Name = "Usunięta", DeletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var sheet = OpenSheet(await new ExportService(db).ExportParishesAsync(default));

        Assert.Equal(new[] { "Nazwa", "Miejscowość" }, Row(sheet, 1, 2));
        Assert.Equal(new[] { "Matki Bożej", "" }, Row(sheet, 2, 2));
        Assert.Equal(new[] { "Św. Pawła", "Bydgoszcz" }, Row(sheet, 3, 2));
        Assert.Equal(3, sheet.LastRowUsed()!.RowNumber());
    }
```

- [ ] **Step 2: Uruchom — ma się nie skompilować.**

- [ ] **Step 3: Implementacja** — w `IExportService` dodaj:

```csharp
    Task<byte[]> ExportMeetingsAsync(CancellationToken ct);
    Task<byte[]> ExportParishesAsync(CancellationToken ct);
```

W `ExportService` dodaj:

```csharp
    public async Task<byte[]> ExportMeetingsAsync(CancellationToken ct)
    {
        var meetings = await _db.Meetings.AsNoTracking()
            .Include(m => m.DokCase).ThenInclude(c => c!.Person)
            .OrderBy(m => m.MeetingDate)
            .ToListAsync(ct);

        return BuildWorkbook(
            "Spotkania",
            new[] { "Data", "Podopieczny / grupa", "Obecność", "Uwagi" },
            meetings.Select(m => new object?[]
            {
                FormatDate(m.MeetingDate),
                m.DokCase?.Person?.FullName ?? m.GroupLabel,
                m.IsAttended is bool attended ? YesNo(attended) : "—",
                m.Notes
            }));
    }

    public async Task<byte[]> ExportParishesAsync(CancellationToken ct)
    {
        var parishes = await _db.Parishes.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

        return BuildWorkbook(
            "Parafie",
            new[] { "Nazwa", "Miejscowość" },
            parishes.Select(p => new object?[] { p.Name, p.City }));
    }
```
Uwaga: `OrderBy(Name)` w InMemory sortuje porządkowo — „Matki Bożej" (M) < „Św. Pawła" (Ś, punkt kodowy wyższy), więc kolejność z testu jest poprawna w InMemory; w SQL Server zależy od collation, ale to nie wpływa na testy.

- [ ] **Step 4: Uruchom — zielone** (`powodzenie: 8`).

- [ ] **Step 5: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Application/Export/IExportService.cs backend/src/DokPortal.Infrastructure/Services/ExportService.cs backend/tests/DokPortal.Infrastructure.Tests/Services/ExportServiceTests.cs
git commit -m "$(cat <<'EOF'
Eksport do Excela: Spotkania i Parafie

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: ExportController, rejestracja DI i testy integracyjne

**Files:**
- Create: `backend/src/DokPortal.Api/Controllers/ExportController.cs`
- Modify: `backend/src/DokPortal.Api/Program.cs` (using + `AddScoped`)
- Test: `backend/tests/DokPortal.Api.IntegrationTests/ExportControllerTests.cs`

**Interfaces:**
- Consumes: `IExportService` z Tasks 1–4 (osiem metod `Export*Async(CancellationToken)`), `IAuditLogService.LogAsync(string userId, string userEmail, string action, string objectDescription, AuditResult result, CancellationToken ct)`, `AppRoles`.
- Produces: `GET /api/export/{people|dok-cases|candidates|missions|formators|supervisions|meetings|parishes}` → plik `.xlsx`.

- [ ] **Step 1: Napisz testy (czerwone)** — `ExportControllerTests.cs`:

```csharp
using System.Net;
using DokPortal.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class ExportControllerTests : IntegrationTestBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public ExportControllerTests(CustomWebApplicationFactory factory) : base(factory)
    {
    }

    [Theory]
    [InlineData("people", "Administrator")]
    [InlineData("people", "DyrektorSKSP")]
    [InlineData("people", "DyrektorDOK")]
    [InlineData("dok-cases", "Administrator")]
    [InlineData("dok-cases", "DyrektorDOK")]
    [InlineData("candidates", "Administrator")]
    [InlineData("candidates", "DyrektorSKSP")]
    [InlineData("missions", "Administrator")]
    [InlineData("missions", "DyrektorSKSP")]
    [InlineData("formators", "Administrator")]
    [InlineData("formators", "DyrektorSKSP")]
    [InlineData("supervisions", "Administrator")]
    [InlineData("supervisions", "DyrektorDOK")]
    [InlineData("supervisions", "DyrektorSKSP")]
    [InlineData("supervisions", "Superwizor")]
    [InlineData("meetings", "Administrator")]
    [InlineData("meetings", "DyrektorDOK")]
    [InlineData("parishes", "Administrator")]
    public async Task Export_ReturnsXlsx_ForAllowedRole(string list, string role)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/export/{list}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(XlsxContentType, response.Content.Headers.ContentType!.MediaType);
        Assert.NotEmpty(await response.Content.ReadAsByteArrayAsync());
    }

    [Theory]
    [InlineData("people")]
    [InlineData("dok-cases")]
    [InlineData("candidates")]
    [InlineData("missions")]
    [InlineData("formators")]
    [InlineData("supervisions")]
    [InlineData("meetings")]
    [InlineData("parishes")]
    public async Task Export_ReturnsForbidden_ForCatechist(string list)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", "KatechistaProwadzacy");

        var response = await client.GetAsync($"/api/export/{list}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("dok-cases", "DyrektorSKSP")]
    [InlineData("candidates", "DyrektorDOK")]
    [InlineData("meetings", "DyrektorSKSP")]
    [InlineData("parishes", "DyrektorDOK")]
    public async Task Export_ReturnsForbidden_ForRoleOutsideListPolicy(string list, string role)
    {
        var client = await CreateAuthenticatedClientAsync($"user-{Guid.NewGuid():N}@example.org", "Sekret123!", role);

        var response = await client.GetAsync($"/api/export/{list}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_ReturnsUnauthorized_WithoutLogin()
    {
        var response = await Client.GetAsync("/api/export/people");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Export_WritesAuditEntry()
    {
        var email = $"user-{Guid.NewGuid():N}@example.org";
        var client = await CreateAuthenticatedClientAsync(email, "Sekret123!", "Administrator");

        var response = await client.GetAsync("/api/export/parishes");
        response.EnsureSuccessStatusCode();

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Contains(db.AuditLogEntries, e => e.UserEmail == email && e.Action == "ExportData" && e.ObjectDescription == "parishes");
    }
}
```

- [ ] **Step 2: Uruchom — czerwone (404 na endpointach)**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Api.IntegrationTests --filter "FullyQualifiedName~ExportControllerTests" 2>&1 | tail -6
```
Expected: kompilacja przechodzi, testy `ReturnsXlsx`/`WritesAuditEntry` padają (404), `Unauthorized` może przejść przypadkiem, a `Forbidden` padają (404).

- [ ] **Step 3: Kontroler** — `ExportController.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using DokPortal.Application.AuditLog;
using DokPortal.Application.Export;
using DokPortal.Domain.Constants;
using DokPortal.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DokPortal.Api.Controllers;

[ApiController]
[Route("api/export")]
[Authorize]
public class ExportController : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IExportService _exportService;
    private readonly IAuditLogService _auditLogService;

    public ExportController(IExportService exportService, IAuditLogService auditLogService)
    {
        _exportService = exportService;
        _auditLogService = auditLogService;
    }

    [HttpGet("people")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP},{AppRoles.DyrektorDOK}")]
    public Task<IActionResult> People(CancellationToken ct) => ExportAsync("people", "osoby", _exportService.ExportPeopleAsync, ct);

    [HttpGet("dok-cases")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK}")]
    public Task<IActionResult> DokCases(CancellationToken ct) => ExportAsync("dok-cases", "podopieczni-dok", _exportService.ExportDokCasesAsync, ct);

    [HttpGet("candidates")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public Task<IActionResult> Candidates(CancellationToken ct) => ExportAsync("candidates", "kandydaci-sksp", _exportService.ExportCandidatesAsync, ct);

    [HttpGet("missions")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public Task<IActionResult> Missions(CancellationToken ct) => ExportAsync("missions", "katechisci-poslani", _exportService.ExportMissionsAsync, ct);

    [HttpGet("formators")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorSKSP}")]
    public Task<IActionResult> Formators(CancellationToken ct) => ExportAsync("formators", "formatorzy", _exportService.ExportFormatorsAsync, ct);

    [HttpGet("supervisions")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK},{AppRoles.DyrektorSKSP},{AppRoles.Superwizor}")]
    public Task<IActionResult> Supervisions(CancellationToken ct) => ExportAsync("supervisions", "superwizje", _exportService.ExportSupervisionsAsync, ct);

    [HttpGet("meetings")]
    [Authorize(Roles = $"{AppRoles.Administrator},{AppRoles.DyrektorDOK}")]
    public Task<IActionResult> Meetings(CancellationToken ct) => ExportAsync("meetings", "spotkania", _exportService.ExportMeetingsAsync, ct);

    [HttpGet("parishes")]
    [Authorize(Roles = AppRoles.Administrator)]
    public Task<IActionResult> Parishes(CancellationToken ct) => ExportAsync("parishes", "parafie", _exportService.ExportParishesAsync, ct);

    private async Task<IActionResult> ExportAsync(
        string listName, string fileName, Func<CancellationToken, Task<byte[]>> export, CancellationToken ct)
    {
        var bytes = await export(ct);

        await _auditLogService.LogAsync(
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value,
            User.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value,
            "ExportData", listName, AuditResult.Allowed, ct);

        return File(bytes, XlsxContentType, $"{fileName}-{DateTime.UtcNow:yyyy-MM-dd}.xlsx");
    }
}
```

W `Program.cs` dodaj `using DokPortal.Application.Export;` (obok innych `using DokPortal.Application...`) i obok `AddScoped<IDashboardService, DashboardService>();`:

```csharp
builder.Services.AddScoped<IExportService, ExportService>();
```

- [ ] **Step 4: Uruchom testy integracyjne — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test tests/DokPortal.Api.IntegrationTests --filter "FullyQualifiedName~ExportControllerTests" 2>&1 | tail -4
```
Expected: wszystkie przechodzą (18 + 8 + 4 + 1 + 1 = 32).

- [ ] **Step 5: Cały backend**

```bash
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "Powodzenie|niepowodzenie"
```
Expected: brak niepowodzeń.

- [ ] **Step 6: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add backend/src/DokPortal.Api/Controllers/ExportController.cs backend/src/DokPortal.Api/Program.cs backend/tests/DokPortal.Api.IntegrationTests/ExportControllerTests.cs
git commit -m "$(cat <<'EOF'
Dodaj ExportController: osiem endpointów eksportu z rolami i audytem

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Frontend — ExportService, konfiguracja list i ExportButtonComponent

**Files:**
- Create: `frontend/src/app/shared/export/export-lists.ts`
- Create: `frontend/src/app/shared/export/export.service.ts`
- Create: `frontend/src/app/shared/export/export-button.component.ts`
- Test: `frontend/src/app/shared/export/export.service.spec.ts`, `frontend/src/app/shared/export/export-button.component.spec.ts`

**Interfaces:**
- Produces:
  - `ExportListKey = 'people' | 'dok-cases' | 'candidates' | 'missions' | 'formators' | 'supervisions' | 'meetings' | 'parishes'`; `EXPORT_LISTS: Record<ExportListKey, { path: string; fileName: string; roles: string[] }>`.
  - `ExportService.download(path: string, fileName: string): Observable<void>` (zapisuje plik; przy błędzie toast `Nie udało się pobrać pliku Excel.` i kończy bez błędu).
  - `<app-export-button list="people" />` — wejście `list: ExportListKey` (wymagane); widoczny tylko gdy `AuthService.hasAnyRole(roles)`.
- Consumes: `AuthService.hasAnyRole(roles: string[]): boolean` (`core/auth/auth.service.ts`), `ToastService.error(message)` (`core/notifications/toast.service.ts`), `environment.apiBaseUrl`.

- [ ] **Step 1: Testy (czerwone)** — `export.service.spec.ts`:

```ts
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ExportService } from './export.service';
import { ToastService } from '../../core/notifications/toast.service';
import { environment } from '../../../environments/environment';

describe('ExportService', () => {
  let service: ExportService;
  let httpMock: HttpTestingController;
  let toast: ToastService;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ExportService);
    httpMock = TestBed.inject(HttpTestingController);
    toast = TestBed.inject(ToastService);
    URL.createObjectURL = vi.fn(() => 'blob:test');
    URL.revokeObjectURL = vi.fn();
  });

  it('downloads the file as a blob and saves it under the given name', () => {
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    service.download('people', 'osoby.xlsx').subscribe();

    const req = httpMock.expectOne(`${environment.apiBaseUrl}/api/export/people`);
    expect(req.request.responseType).toBe('blob');
    req.flush(new Blob(['x']));

    expect(URL.createObjectURL).toHaveBeenCalled();
    expect(click).toHaveBeenCalledTimes(1);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:test');
    click.mockRestore();
  });

  it('shows an error toast and completes when the request fails', () => {
    let completed = false;

    service.download('people', 'osoby.xlsx').subscribe({ complete: () => (completed = true) });

    httpMock.expectOne(`${environment.apiBaseUrl}/api/export/people`).flush('boom', { status: 500, statusText: 'Server Error' });

    expect(completed).toBe(true);
    expect(toast.toasts()[0].kind).toBe('error');
  });
});
```

`export-button.component.spec.ts` (sprawdź w `core/auth/auth.service.spec.ts`, jak istniejące testy ustawiają rolę/token; poniższy test podmienia `AuthService` prostym stubem):

```ts
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { ExportButtonComponent } from './export-button.component';
import { AuthService } from '../../core/auth/auth.service';
import { environment } from '../../../environments/environment';

describe('ExportButtonComponent', () => {
  let fixture: ComponentFixture<ExportButtonComponent>;
  let httpMock: HttpTestingController;

  function setup(allowed: boolean) {
    TestBed.configureTestingModule({
      imports: [ExportButtonComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: AuthService, useValue: { hasAnyRole: () => allowed } }
      ]
    });
    fixture = TestBed.createComponent(ExportButtonComponent);
    fixture.componentRef.setInput('list', 'people');
    httpMock = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  beforeEach(() => {
    URL.createObjectURL = vi.fn(() => 'blob:test');
    URL.revokeObjectURL = vi.fn();
  });

  it('is hidden for users without an allowed role', () => {
    setup(false);

    expect(fixture.nativeElement.querySelector('button')).toBeNull();
  });

  it('downloads the list when clicked by an allowed user', () => {
    setup(true);
    const click = vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {});

    fixture.nativeElement.querySelector('button').click();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('button').disabled).toBe(true);
    httpMock.expectOne(`${environment.apiBaseUrl}/api/export/people`).flush(new Blob(['x']));
    fixture.detectChanges();

    expect(click).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.querySelector('button').disabled).toBe(false);
    click.mockRestore();
  });
});
```

- [ ] **Step 2: Uruchom — czerwone (brak modułów)**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -12
```

- [ ] **Step 3: Implementacja**

`export-lists.ts`:

```ts
export type ExportListKey =
  | 'people' | 'dok-cases' | 'candidates' | 'missions' | 'formators' | 'supervisions' | 'meetings' | 'parishes';

export interface ExportListConfig {
  path: string;
  fileName: string;
  roles: string[];
}

export const EXPORT_LISTS: Record<ExportListKey, ExportListConfig> = {
  people: { path: 'people', fileName: 'osoby.xlsx', roles: ['Administrator', 'DyrektorSKSP', 'DyrektorDOK'] },
  'dok-cases': { path: 'dok-cases', fileName: 'podopieczni-dok.xlsx', roles: ['Administrator', 'DyrektorDOK'] },
  candidates: { path: 'candidates', fileName: 'kandydaci-sksp.xlsx', roles: ['Administrator', 'DyrektorSKSP'] },
  missions: { path: 'missions', fileName: 'katechisci-poslani.xlsx', roles: ['Administrator', 'DyrektorSKSP'] },
  formators: { path: 'formators', fileName: 'formatorzy.xlsx', roles: ['Administrator', 'DyrektorSKSP'] },
  supervisions: { path: 'supervisions', fileName: 'superwizje.xlsx', roles: ['Administrator', 'DyrektorDOK', 'DyrektorSKSP', 'Superwizor'] },
  meetings: { path: 'meetings', fileName: 'spotkania.xlsx', roles: ['Administrator', 'DyrektorDOK'] },
  parishes: { path: 'parishes', fileName: 'parafie.xlsx', roles: ['Administrator'] }
};
```

`export.service.ts`:

```ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ToastService } from '../../core/notifications/toast.service';

@Injectable({ providedIn: 'root' })
export class ExportService {
  constructor(
    private readonly http: HttpClient,
    private readonly toast: ToastService
  ) {}

  download(path: string, fileName: string): Observable<void> {
    return this.http.get(`${environment.apiBaseUrl}/api/export/${path}`, { responseType: 'blob' }).pipe(
      map(blob => this.save(blob, fileName)),
      catchError(() => {
        this.toast.error('Nie udało się pobrać pliku Excel.');
        return of(undefined);
      })
    );
  }

  private save(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  }
}
```

`export-button.component.ts`:

```ts
import { Component, Input, signal } from '@angular/core';
import { AuthService } from '../../core/auth/auth.service';
import { EXPORT_LISTS, ExportListKey } from './export-lists';
import { ExportService } from './export.service';

@Component({
  selector: 'app-export-button',
  standalone: true,
  template: `
    @if (canExport()) {
      <button class="btn ghost" [disabled]="exporting()" (click)="export()">
        {{ exporting() ? 'Pobieranie…' : 'Eksportuj do Excela' }}
      </button>
    }
  `
})
export class ExportButtonComponent {
  @Input({ required: true }) list!: ExportListKey;

  readonly exporting = signal(false);

  constructor(
    private readonly auth: AuthService,
    private readonly exportService: ExportService
  ) {}

  canExport(): boolean {
    return this.auth.hasAnyRole(EXPORT_LISTS[this.list].roles);
  }

  export(): void {
    const config = EXPORT_LISTS[this.list];
    this.exporting.set(true);
    this.exportService.download(config.path, config.fileName).subscribe({
      complete: () => this.exporting.set(false)
    });
  }
}
```

- [ ] **Step 4: Uruchom — zielone**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -8
```
Expected: wszystkie pliki testowe przechodzą (poprzednio 37 plików / 43 testy + 2 nowe pliki / 4 testy).

- [ ] **Step 5: Commit**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/shared/export
git commit -m "$(cat <<'EOF'
Dodaj ExportService i ExportButtonComponent (frontend)

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Przycisk na ośmiu listach, wpis „Co nowego", weryfikacja i push

**Files (modify):**
- `frontend/src/app/features/people/people-list.component.{ts,html}`
- `frontend/src/app/features/dok-cases/dok-cases-list.component.{ts,html}`
- `frontend/src/app/features/candidates/candidates-list.component.{ts,html}`
- `frontend/src/app/features/missions/missions-list.component.{ts,html}`
- `frontend/src/app/features/formators/formators-list.component.{ts,html}`
- `frontend/src/app/features/supervisions/supervisions-list.component.{ts,html}`
- `frontend/src/app/features/meetings/meetings-list.component.{ts,html}`
- `frontend/src/app/features/parish-board/parishes-list.component.{ts,html}`
- `frontend/src/app/features/dashboard/dashboard.component.ts` (changelog)

**Interfaces:**
- Consumes: `<app-export-button list="…" />` i `ExportButtonComponent` (import `../../shared/export/export-button.component`) z Task 6.

- [ ] **Step 1: Dla każdej z ośmiu list** — w pliku `.ts` dodaj `import { ExportButtonComponent } from '../../shared/export/export-button.component';` i dopisz `ExportButtonComponent` do tablicy `imports` dekoratora `@Component`. W pliku `.html` zamień pierwszy blok nagłówka, np. dla osób:

```html
<div class="page-heading">
  <div><h2>Baza osób</h2><p>Jeden profil osoby, wiele ról i powiązań.</p></div>
  <button class="btn primary" (click)="openAddForm()">＋ Dodaj osobę</button>
</div>
```
na:
```html
<div class="page-heading">
  <div><h2>Baza osób</h2><p>Jeden profil osoby, wiele ról i powiązań.</p></div>
  <div style="display:flex;gap:8px;align-items:center">
    <app-export-button list="people" />
    <button class="btn primary" (click)="openAddForm()">＋ Dodaj osobę</button>
  </div>
</div>
```
Klucze `list`: people, dok-cases, candidates, missions, formators, supervisions, meetings, parishes (kolejno dla plików z listy wyżej; `parishes-list` → `parishes`). Zachowaj istniejący tekst nagłówka i przycisku każdej listy bez zmian.

- [ ] **Step 2: Wpis w „Co nowego"** — w `dashboard.component.ts` dodaj na początku tablicy `changelog`:

```ts
    { date: '2026-10-02', text: 'Eksport do Excela — na listach (Osoby, Podopieczni DOK, Kandydaci SKŚP, Katechiści posłani, Formatorzy, Superwizje, Spotkania, Parafie) pojawił się przycisk „Eksportuj do Excela". Widzą go tylko osoby z odpowiednimi uprawnieniami, a każdy eksport zapisuje się w dzienniku audytu.' },
```

- [ ] **Step 3: Testy frontendu i backendu**

```bash
cd C:/eu02_install/DOKPortalLight/frontend && npx ng test --watch=false 2>&1 | tail -8
cd C:/eu02_install/DOKPortalLight/backend && dotnet test 2>&1 | grep -E "Powodzenie|niepowodzenie"
```
Expected: zielone. Jeśli któryś spec listy padnie przez brak dostawcy (np. `HttpClient`), dodaj `provideHttpClient(), provideHttpClientTesting()` do jego `TestBed`.

- [ ] **Step 4: Weryfikacja ręczna w przeglądarce (jeśli backend lokalny dostępny)** — uruchom front i backend, zaloguj się jako Administrator, wejdź na „Baza osób", kliknij „Eksportuj do Excela", sprawdź, że pobrał się `osoby.xlsx` z nagłówkami po polsku; zaloguj się jako katechista i potwierdź, że przycisku nie ma. Jeśli środowisko lokalne nie jest dostępne, zaznacz to w podsumowaniu.

- [ ] **Step 5: Commit i push**

```bash
cd C:/eu02_install/DOKPortalLight && git add frontend/src/app/features docs/superpowers/plans/2026-10-02-excel-export.md
git status --short
git commit -m "$(cat <<'EOF'
Dodaj przycisk eksportu do Excela na ośmiu listach

Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>
EOF
)"
git push origin master
```
Przed commitem sprawdź `git status --short`: w stage'u mają być tylko pliki z `frontend/src/app/features` i plan (bez `appsettings.Development.json` i `.claude/`).

---

## Self-review

- **Pokrycie specyfikacji:** backend (ClosedXML, `IExportService`, `BuildWorkbook` z pogrubionym nagłówkiem/zamrożonym wierszem/autoszerokością) — Task 1; osiem list z kolumnami i polskimi etykietami — Tasks 1–4; kontroler, role per endpoint, audyt, DI — Task 5; testy xUnit/integracyjne (200, 403 dla katechisty i ról spoza polityki, 401, audyt) — Tasks 1–5; frontend (`ExportService`, przycisk z rolami, osiem list) — Tasks 6–7; wpis „Co nowego" — Task 7. Pominięcie `Person.Notes` i rekordów usuniętych — test w Task 1 (oraz pozostałe testy z rekordem usuniętym).
- **Placeholdery:** brak.
- **Spójność typów:** `ExportPeopleAsync`…`ExportParishesAsync` (po jednym `CancellationToken`) zgodne między interfejsem, serwisem, testami i kontrolerem (`Func<CancellationToken, Task<byte[]>>`); klucze `ExportListKey` zgodne ze ścieżkami endpointów; `ExportService.download(path, fileName)` zgodne z użyciem w przycisku.
