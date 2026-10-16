using System.IO.Compression;
using DocumentFormat.OpenXml.Packaging;
using DokPortal.Application.Common;
using DokPortal.Application.Documents;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Documents;
using DokPortal.Infrastructure.Identity;
using DokPortal.Infrastructure.Persistence;
using DokPortal.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DokPortal.Infrastructure.Tests.Services;

/// <summary>Wzory pism w portalu: pola, układ, PDF i Word, wzory startowe, nagłówek.</summary>
public class LetterTemplateTests
{
    private sealed class Storage : IFileStorageService
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public async Task UploadAsync(string blobPath, Stream content, string contentType, CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            Files[blobPath] = ms.ToArray();
        }
        public Task<StoredFile?> DownloadAsync(string blobPath, CancellationToken ct) =>
            Task.FromResult(Files.TryGetValue(blobPath, out var b) ? new StoredFile(new MemoryStream(b), "image/png") : null);
        public Task DeleteAsync(string blobPath, CancellationToken ct) { Files.Remove(blobPath); return Task.CompletedTask; }
    }

    private static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static bool IsPdf(byte[] b) => b.Length > 4 && b[0] == '%' && b[1] == 'P' && b[2] == 'D' && b[3] == 'F';

    private static string DocxText(byte[] bytes)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(bytes), false);
        var text = doc.MainDocumentPart!.Document.Body!.InnerText;
        var header = string.Join(" ", doc.MainDocumentPart.HeaderParts.Select(h => h.Header.InnerText));
        var footer = string.Join(" ", doc.MainDocumentPart.FooterParts.Select(f => f.Footer.InnerText));
        return text + " | " + header + " | " + footer;
    }

    private static async Task<(Person Person, string UserId)> SeedAsync(AppDbContext db)
    {
        var parish = new Parish { Id = Guid.NewGuid(), Name = "św. Józefa", City = "Bydgoszcz", Address = "ul. Kościelna 1" };
        var pastor = new Person { Id = Guid.NewGuid(), FirstName = "Adam", LastName = "Proboszcz", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var catechist = new Person { Id = Guid.NewGuid(), FirstName = "Anna", LastName = "Maj", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        var person = new Person
        {
            Id = Guid.NewGuid(), FirstName = "Jan", LastName = "Kowalski", BirthDate = new DateOnly(1990, 5, 12), ParishId = parish.Id,
            AddressStreet = "ul. Długa 5", AddressPostalCode = "85-001", AddressCity = "Bydgoszcz", Email = "jan@example.org",
            CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow
        };
        var issuer = new Person { Id = Guid.NewGuid(), FirstName = "Szymon", LastName = "Łuczak", CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        db.Parishes.Add(parish);
        db.People.AddRange(pastor, catechist, person, issuer);
        db.PersonFunctions.Add(new PersonFunction { Id = Guid.NewGuid(), PersonId = pastor.Id, Type = FunctionType.Pastor, ParishId = parish.Id });
        db.DokCases.Add(new DokCase
        {
            Id = Guid.NewGuid(), PersonId = person.Id, CatechistPersonId = catechist.Id, Path = DokPath.Confirmation, Stage = DokStage.Catechumenate,
            StageSinceUtc = DateTime.UtcNow, CreatedAtUtc = new DateTime(2025, 9, 1), UpdatedAtUtc = DateTime.UtcNow
        });
        db.Users.Add(new AppUser { Id = "u1", UserName = "s@example.org", Email = "s@example.org", PersonId = issuer.Id });
        await db.SaveChangesAsync();
        return (person, "u1");
    }

    private const string Body =
        "<p style=\"text-align: right\">{{pismo.miejscowość}}, dnia {{pismo.data}}</p>" +
        "<h2 style=\"text-align: center\">Zaświadczenie</h2>" +
        "<p style=\"text-align: justify\">Pan <strong>{{osoba.imię i nazwisko}}</strong>, ur. {{osoba.data urodzenia}}, zam. {{osoba.adres}}, " +
        "parafia {{osoba.parafia}} (proboszcz {{proboszcz.imię i nazwisko}}), ścieżka <em>{{dok.ścieżka}}</em>, katechista <u>{{dok.katechista}}</u>.</p>" +
        "<ul><li><p>Chrzest: {{wpisz:Data chrztu}}</p></li><li><p>Misja: {{misja.miejsce posługi}}</p></li></ul>" +
        "<ol><li><p>pierwszy</p></li><li><p>drugi</p></li></ol><hr><p>Wystawił: {{pismo.wystawił}}<br>{{pismo.uwagi}}</p><script>alert(1)</script>";

    [Fact]
    public async Task FieldsAreFilledFromThePersonTheirCaseAndParish_AndMissingOnesLeaveDots()
    {
        await using var db = CreateContext();
        var (person, user) = await SeedAsync(db);
        var values = await LetterFields.ResolveAsync(db, person.Id, "Bydgoszcz", "Szymon Łuczak", new DateTime(2026, 10, 16), default);
        var missing = new List<string>();

        var filled = LetterFields.Fill(Body, values, new Dictionary<string, string> { ["data chrztu"] = "1 maja 1990 <b>" }, missing);

        Assert.Contains("Bydgoszcz, dnia 16.10.2026", filled);
        Assert.Contains("<strong>Jan Kowalski</strong>, ur. 12.05.1990, zam. ul. Długa 5, 85-001 Bydgoszcz", filled);
        Assert.Contains("parafia św. Józefa (Bydgoszcz) (proboszcz Adam Proboszcz)", filled);
        Assert.Contains("<em>Bierzmowanie</em>", filled);
        Assert.Contains("<u>Anna Maj</u>", filled);
        Assert.Contains("Chrzest: 1 maja 1990 &lt;b&gt;", filled);
        Assert.Contains("Misja: " + LetterFields.Blank, filled);
        Assert.Equal(new[] { "Miejsce posługi (misja)", "Uwagi wpisane przy generowaniu" }, missing);
        Assert.Equal("16 października 2026", LetterFields.Words(new DateTime(2026, 10, 16)));
        Assert.Equal(new[] { "Data chrztu" }, LetterFields.Inputs(Body));
        Assert.Equal(new[] { "osoba.imie" }, LetterFields.Unknown("<p>{{osoba.imie}} {{osoba.imię}} {{wpisz:Coś}}</p>"));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => LetterFields.ResolveAsync(db, Guid.NewGuid(), "", "", DateTime.UtcNow, default));
    }

    [Fact]
    public void TheEditorHtml_BecomesBlocks_WithoutScripts()
    {
        var blocks = LetterLayout.Parse(Body + "<div>luźny <b>tekst</b></div>tekst bez akapitu<p></p>");

        Assert.Equal(LetterAlign.Right, blocks[0].Align);
        Assert.Equal((LetterBlockKind.Heading2, LetterAlign.Center), (blocks[1].Kind, blocks[1].Align));
        Assert.Equal(LetterAlign.Justify, blocks[2].Align);
        Assert.Contains(blocks[2].Runs, r => r.Bold && r.Text.Contains("osoba.imię i nazwisko"));
        Assert.Contains(blocks[2].Runs, r => r.Italic);
        Assert.Contains(blocks[2].Runs, r => r.Underline);
        Assert.Equal(new[] { LetterBlockKind.Bullet, LetterBlockKind.Bullet, LetterBlockKind.Numbered, LetterBlockKind.Numbered, LetterBlockKind.Rule },
            blocks.Skip(3).Take(5).Select(b => b.Kind));
        Assert.Equal(2, blocks[6].Number);
        Assert.Contains(blocks[8].Runs, r => r.Break);
        Assert.DoesNotContain(blocks.SelectMany(b => b.Runs), r => r.Text.Contains("alert"));
        Assert.Contains(blocks, b => b.Runs.Any(r => r.Bold && r.Text == "tekst"));
        Assert.Contains(blocks, b => b.Runs.Any(r => r.Text.Contains("tekst bez akapitu")));
        Assert.Equal(LetterBlockKind.Paragraph, blocks[^1].Kind);
    }

    [Fact]
    public void TheLetterRenders_AsPdfAndAsWord_WithTheLetterhead()
    {
        var blocks = LetterLayout.Parse(Body.Replace("{{", "").Replace("}}", ""));
        var head = new LetterheadContent("Diecezjalny Ośrodek Katechumenalny\nul. Grodzka 18", "tel. 52 000 00 00", Png);

        Assert.True(IsPdf(LetterPdf.Render(blocks, head)));
        Assert.True(IsPdf(LetterPdf.Render(blocks, null)));
        var word = LetterDocx.Render(blocks, head);
        var text = DocxText(word);
        Assert.Contains("Zaświadczenie", text);
        Assert.Contains("•", text);
        Assert.Contains("2.", text);
        Assert.Contains("Diecezjalny Ośrodek Katechumenalny", text);
        Assert.Contains("tel. 52 000 00 00", text);
        using var zip = new ZipArchive(new MemoryStream(word));
        Assert.True(zip.Entries.Any(e => e.FullName.EndsWith(".png")), string.Join(", ", zip.Entries.Select(e => e.FullName)));
        Assert.DoesNotContain("Diecezjalny", DocxText(LetterDocx.Render(blocks, null)));
    }

    [Fact]
    public async Task StarterTemplates_AreCreatedOnce_AndCanBeEditedDisabledAndDeletedUntilUsed()
    {
        await using var db = CreateContext();
        var (person, user) = await SeedAsync(db);
        var service = new LetterTemplateService(db);

        var starters = await service.ListAsync(false, default);
        Assert.Equal(11, starters.Count);
        Assert.Contains(starters, t => t.Name == "Zaświadczenie o chrzcie" && t.Inputs.Contains("Data chrztu"));
        Assert.All(starters, t => Assert.Empty(t.UnknownFields));

        var created = await service.CreateAsync(new SaveLetterTemplateRequest { Name = " Podziękowanie ", Body = "<p>Dziękujemy {{osoba.imie}}</p>" }, "s@example.org", default);
        Assert.Equal(("Podziękowanie", 11), (created.Name, created.SortOrder));
        Assert.Equal(new[] { "osoba.imie" }, created.UnknownFields);

        var updated = await service.UpdateAsync(created.Id, new SaveLetterTemplateRequest { Name = "Podziękowanie", Body = "<p>Dziękujemy {{osoba.imię}}</p>", IsActive = false, UseLetterhead = false }, "x@example.org", default);
        Assert.Equal((false, false, "x@example.org"), (updated!.IsActive, updated.UseLetterhead, updated.UpdatedByEmail));
        Assert.Equal(11, (await service.ListAsync(false, default)).Count);
        Assert.Equal(12, (await service.ListAsync(true, default)).Count);
        Assert.Null(await service.UpdateAsync(Guid.NewGuid(), new SaveLetterTemplateRequest { Name = "x", Body = "<p>x</p>" }, null, default));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new SaveLetterTemplateRequest { Name = "", Body = "<p>x</p>" }, null, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new SaveLetterTemplateRequest { Name = "Pusty", Body = "<p> </p>" }, null, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(new SaveLetterTemplateRequest { Name = new string('x', 201), Body = "<p>x</p>" }, null, default));

        var baptism = starters.Single(t => t.Name == "Zaświadczenie o chrzcie");
        await new DocumentService(db).GenerateAsync(new GenerateDocumentRequest { LetterTemplateId = baptism.Id, PersonId = person.Id }, user, default);
        var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(baptism.Id, default));
        Assert.Contains("wyłączyć", refused.Message);
        Assert.Equal(1, (await service.GetAsync(baptism.Id, default))!.UsageCount);
        Assert.True(await service.DeleteAsync(created.Id, default));
        Assert.False(await service.DeleteAsync(created.Id, default));
        Assert.Null(await service.GetAsync(created.Id, default));

        // Usunięte wzory startowe nie wracają.
        foreach (var t in await db.LetterTemplates.Where(t => t.Id != baptism.Id).ToListAsync()) db.LetterTemplates.Remove(t);
        await db.SaveChangesAsync();
        Assert.Single(await service.ListAsync(true, default));
        Assert.NotEmpty(service.Fields());
    }

    [Fact]
    public async Task GeneratingAWordLetter_FillsTheInputs_KeepsASnapshot_AndOffersBothFormatsLater()
    {
        await using var db = CreateContext();
        var (person, user) = await SeedAsync(db);
        var storage = new Storage();
        var templates = new LetterTemplateService(db, storage);
        var template = await templates.CreateAsync(new SaveLetterTemplateRequest { Name = "Zaświadczenie o chrzcie", Body = Body }, null, default);
        var documents = new DocumentService(db, storage);

        var result = await documents.GenerateAsync(new GenerateDocumentRequest
        {
            LetterTemplateId = template.Id, PersonId = person.Id, Format = "docx", AdditionalNotes = "Do akt",
            Inputs = new Dictionary<string, string> { ["Data chrztu"] = "1 maja 1990" }
        }, user, default);

        Assert.Equal(LetterRenderer.DocxType, result!.File.ContentType);
        Assert.Matches(@"^Zaswiadczenie-o-chrzcie_Kowalski_\d{4}-\d{2}-\d{2}\.docx$", result.File.FileName);
        var text = DocxText(result.File.Bytes);
        Assert.Contains("Jan Kowalski", text);
        Assert.Contains("1 maja 1990", text);
        Assert.Contains("Wystawił: Szymon Łuczak", text);
        Assert.Contains("Do akt", text);
        Assert.Equal(new[] { "Miejsce posługi (misja)" }, result.File.MissingFields);
        Assert.Equal(("Zaświadczenie o chrzcie", true), (result.History.Title, result.History.CanDownloadWord));
        Assert.Single(storage.Files.Keys, k => k.StartsWith("generated-documents/"));

        // Zmiana wzoru i danych osoby nie zmienia wydanego pisma.
        await templates.UpdateAsync(template.Id, new SaveLetterTemplateRequest { Name = "Inna nazwa", Body = "<p>inna treść</p>" }, null, default);
        var stored = await db.People.SingleAsync(p => p.Id == person.Id);
        stored.LastName = "Zmieniony";
        await db.SaveChangesAsync();
        storage.Files.Clear();
        var word = await documents.DownloadAsync(result.History.Id, default, "docx");
        Assert.Contains("Jan Kowalski", DocxText(word!.Bytes));
        Assert.StartsWith("Zaswiadczenie-o-chrzcie_Zmieniony_", word.FileName);
        var pdf = await documents.DownloadAsync(result.History.Id, default, "pdf");
        Assert.True(IsPdf(pdf!.Bytes));
        Assert.False(pdf.Restored);
    }

    [Fact]
    public async Task LegacyTypes_UseTheirStarterTemplate_AndDisabledOrMissingTemplatesAreRefused()
    {
        await using var db = CreateContext();
        var (person, user) = await SeedAsync(db);
        var documents = new DocumentService(db);

        var legacy = await documents.GenerateAsync(new GenerateDocumentRequest { Template = DocumentTemplate.SkspCompletionCertificate, PersonId = person.Id }, user, default);
        Assert.True(IsPdf(legacy!.File.Bytes));
        Assert.Equal((DocumentTemplate?)DocumentTemplate.SkspCompletionCertificate, legacy.History.Template);
        Assert.Contains("Cel zaświadczenia", legacy.File.MissingFields);

        var template = await db.LetterTemplates.FirstAsync(t => t.LegacyType == DocumentTemplate.GdprClause);
        template.IsActive = false;
        await db.SaveChangesAsync();
        var disabled = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            documents.GenerateAsync(new GenerateDocumentRequest { LetterTemplateId = template.Id, PersonId = person.Id }, user, default));
        Assert.Contains("wyłączony", disabled.Message);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            documents.GenerateAsync(new GenerateDocumentRequest { LetterTemplateId = Guid.NewGuid(), PersonId = person.Id }, user, default));
        Assert.Null(await documents.GenerateAsync(new GenerateDocumentRequest { Template = DocumentTemplate.DokReferral, PersonId = Guid.NewGuid() }, user, default));

        db.GeneratedDocuments.Add(new GeneratedDocument { Id = Guid.NewGuid(), Template = DocumentTemplate.DokReferral, PersonId = person.Id, GeneratedByUserId = user, CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var old = await db.GeneratedDocuments.SingleAsync(d => d.RenderedBody == null);
        var wordRefused = await Assert.ThrowsAsync<InvalidOperationException>(() => documents.DownloadAsync(old.Id, default, "docx"));
        Assert.Contains("tylko dla pism", wordRefused.Message);
        Assert.False((await documents.GetHistoryAsync(default)).Single(d => d.Id == old.Id).CanDownloadWord);
    }

    [Fact]
    public async Task ThePreview_UsesExamplesOrAChosenPerson_AndTheLetterheadWithItsLogo()
    {
        await using var db = CreateContext();
        var (person, user) = await SeedAsync(db);
        var storage = new Storage();
        var service = new LetterTemplateService(db, storage);

        var head = await service.SaveLetterheadAsync(new SaveLetterheadRequest { HeaderText = " DOK\r\nul. Grodzka 18 ", FooterText = "stopka", Place = "Nakło" }, default);
        Assert.Equal(("DOK\nul. Grodzka 18", "Nakło", false), (head.HeaderText, head.Place, head.HasLogo));
        head = await service.SaveLogoAsync(new MemoryStream(Png), "image/png", Png.Length, default);
        Assert.True(head.HasLogo);
        Assert.Equal("image/png", (await service.GetLogoAsync(default))!.Value.ContentType);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveLogoAsync(new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }), "image/gif", 5, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveLogoAsync(new MemoryStream(Png), "image/png", 3 * 1024 * 1024, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => new LetterTemplateService(db).SaveLogoAsync(new MemoryStream(Png), "image/png", 10, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveLetterheadAsync(new SaveLetterheadRequest { Place = new string('x', 101) }, default));

        var example = await service.PreviewAsync(new PreviewLetterRequest { Body = Body, Format = "docx" }, user, default);
        var exampleText = DocxText(example.Bytes);
        Assert.Contains("Jan Kowalski", exampleText);
        Assert.Contains("[Data chrztu]", exampleText);
        Assert.Contains("Nakło, dnia", exampleText);
        Assert.Contains("ul. Grodzka 18", exampleText);
        Assert.Contains("stopka", exampleText);

        var real = await service.PreviewAsync(new PreviewLetterRequest { Body = Body, PersonId = person.Id, UseLetterhead = false }, user, default);
        Assert.True(IsPdf(real.Bytes));
        Assert.Contains("Data chrztu", real.MissingFields);
        Assert.StartsWith("podglad_Kowalski_", real.FileName);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync(new PreviewLetterRequest { Body = Body, PersonId = Guid.NewGuid() }, user, default));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync(new PreviewLetterRequest { Body = " " }, user, default));

        head = await service.DeleteLogoAsync(default);
        Assert.False(head.HasLogo);
        Assert.Null(await service.GetLogoAsync(default));
        Assert.Equal("Nakło", (await service.GetLetterheadAsync(default)).Place);
    }
}
