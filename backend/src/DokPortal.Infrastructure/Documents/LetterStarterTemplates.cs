using DokPortal.Domain.Documents;
using DokPortal.Domain.Entities;
using DokPortal.Domain.Enums;
using DokPortal.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DokPortal.Infrastructure.Documents;

/// <summary>
/// Wzory startowe – robocze wersje dawnych wbudowanych pism. Każdy ma akapit „WZÓR DO UZUPEŁNIENIA”,
/// który usuwa się po zatwierdzeniu treści.
/// </summary>
public static class LetterStarterTemplates
{
    private const string Date = "<p style=\"text-align: right\">{{pismo.miejscowość}}, dnia {{pismo.data słownie}}</p><p></p>";
    private const string Draft = "<p></p><p><em>WZÓR DO UZUPEŁNIENIA – popraw treść w „Dokumenty i pisma” → „Wzory pism” i usuń ten akapit.</em></p>";
    private const string Signature = "<p></p><p></p><p style=\"text-align: right\">……………………………………</p><p style=\"text-align: right\">(podpis i pieczęć)</p>";
    private const string Sender = "<p>{{osoba.imię i nazwisko}}<br>{{osoba.adres}}<br>tel. {{osoba.telefon}}</p><p></p>";

    private static string Title(string title) => $"<h2 style=\"text-align: center\">{title}</h2><p></p>";

    private static string Certificate(string title, string statement) =>
        Date + Title(title) +
        "<p style=\"text-align: justify\">Zaświadcza się, że {{rodzaj:Pan|Pani}} <strong>{{osoba.imię i nazwisko}}</strong>, {{rodzaj:urodzony|urodzona}} {{osoba.data urodzenia}}, " +
        "{{rodzaj:zamieszkały|zamieszkała}}: {{osoba.adres}}, parafia {{osoba.parafia}}, " + statement + "</p>" +
        "<p style=\"text-align: justify\">Zaświadczenie wydaje się na prośbę {{rodzaj:zainteresowanego|zainteresowanej}} w celu przedłożenia {{wpisz:Cel zaświadczenia}}.</p>" +
        Signature + Draft;

    public static IReadOnlyList<(DocumentTemplate Type, string Body)> Bodies { get; } = new (DocumentTemplate, string)[]
    {
        (DocumentTemplate.LetterToBishop,
            Date + Sender +
            "<p style=\"text-align: right\"><strong>Jego Ekscelencja<br>Ksiądz Biskup Diecezjalny<br>Diecezji Bydgoskiej</strong></p><p></p>" +
            "<p style=\"text-align: justify\">Ekscelencjo, Najdostojniejszy Księże Biskupie,</p>" +
            "<p style=\"text-align: justify\">{{wpisz:Treść prośby}}</p>" +
            "<p style=\"text-align: justify\">Z wyrazami szacunku i prośbą o modlitwę,</p>" + Signature + Draft),
        (DocumentTemplate.ConversionConsent,
            Date + Title("Zgoda na konwersję") +
            "<p style=\"text-align: justify\">Ja, {{rodzaj:niżej podpisany|niżej podpisana}} <strong>{{osoba.imię i nazwisko}}</strong>, {{rodzaj:urodzony|urodzona}} {{osoba.data urodzenia}}, " +
            "{{rodzaj:zamieszkały|zamieszkała}}: {{osoba.adres}}, dotychczas {{rodzaj:należący|należąca}} do {{wpisz:Dotychczasowy Kościół / wspólnota}}, " +
            "proszę o przyjęcie do pełnej wspólnoty Kościoła katolickiego.</p>" +
            "<p style=\"text-align: justify\">Przygotowanie prowadzi {{dok.katechista}} w Diecezjalnym Ośrodku Katechumenalnym.</p>" + Signature + Draft),
        (DocumentTemplate.CanonicalMissionApplication,
            Date + Sender + Title("Wniosek o udzielenie misji kanonicznej") +
            "<p style=\"text-align: justify\">Zwracam się z prośbą o udzielenie misji kanonicznej {{rodzaj:Panu|Pani}} <strong>{{osoba.imię i nazwisko|celownik}}</strong>, " +
            "{{rodzaj:który ukończył|która ukończyła}} formację w Szkole Katechistów św. Pawła w dniu {{sksp.data ukończenia}}, " +
            "do posługi katechisty w: {{wpisz:Proponowane miejsce posługi}}.</p>" +
            "<p style=\"text-align: justify\">Proboszcz parafii zamieszkania: {{proboszcz.imię i nazwisko}} ({{osoba.parafia}}).</p>" + Signature + Draft),
        (DocumentTemplate.DokReferral,
            Date + Title("Skierowanie do Diecezjalnego Ośrodka Katechumenalnego") +
            "<p style=\"text-align: justify\">Kieruję {{rodzaj:Pana|Panią}} <strong>{{osoba.imię i nazwisko|biernik}}</strong>, {{rodzaj:urodzonego|urodzoną}} {{osoba.data urodzenia}}, " +
            "{{rodzaj:zamieszkałego|zamieszkałą}}: {{osoba.adres}}, na przygotowanie w Diecezjalnym Ośrodku Katechumenalnym – ścieżka: {{dok.ścieżka}}.</p>" +
            "<p>Proboszcz parafii: {{proboszcz.imię i nazwisko}}, {{osoba.parafia}}</p>" + Signature + Draft),
        (DocumentTemplate.SkspCompletionCertificate, Certificate("Zaświadczenie o ukończeniu Szkoły Katechistów",
            "{{rodzaj:ukończył|ukończyła}} trzyletnią formację w Szkole Katechistów św. Pawła w Bydgoszczy w dniu {{sksp.data ukończenia}}.")),
        (DocumentTemplate.CatechumenateStudyCertificate, Certificate("Zaświadczenie o ukończeniu studium katechumenalnego",
            "{{rodzaj:ukończył|ukończyła}} przygotowanie w Diecezjalnym Ośrodku Katechumenalnym (ścieżka: {{dok.ścieżka}}), prowadzone przez {{dok.katechista}}.")),
        (DocumentTemplate.BaptismCertificate, Certificate("Zaświadczenie o przyjęciu sakramentu chrztu",
            "{{rodzaj:przyjął|przyjęła}} sakrament chrztu świętego w dniu {{wpisz:Data chrztu}} w parafii {{wpisz:Parafia chrztu}}.")),
        (DocumentTemplate.ConfirmationCertificate, Certificate("Zaświadczenie o przyjęciu sakramentu bierzmowania",
            "{{rodzaj:przyjął|przyjęła}} sakrament bierzmowania w dniu {{wpisz:Data bierzmowania}} w parafii {{wpisz:Parafia bierzmowania}}.")),
        (DocumentTemplate.EucharistCertificate, Certificate("Zaświadczenie o przyjęciu Pierwszej Komunii Świętej",
            "{{rodzaj:przyjął|przyjęła}} Pierwszą Komunię Świętą w dniu {{wpisz:Data Pierwszej Komunii}} w parafii {{wpisz:Parafia}}.")),
        (DocumentTemplate.GodparentCertificate, Certificate("Zaświadczenie dla kandydata na ojca chrzestnego / matkę chrzestną",
            "spełnia wymagania stawiane chrzestnym i może pełnić tę funkcję podczas chrztu: {{wpisz:Imię i nazwisko chrzczonego}}.")),
        (DocumentTemplate.GdprClause,
            Title("Klauzula informacyjna o przetwarzaniu danych osobowych") +
            "<p style=\"text-align: justify\">Zgodnie z Dekretem ogólnym Konferencji Episkopatu Polski w sprawie ochrony osób fizycznych w związku z przetwarzaniem danych osobowych w Kościele katolickim informujemy, że:</p>" +
            "<ol><li><p>Administratorem danych {{rodzaj:Pana|Pani}} <strong>{{osoba.imię i nazwisko|dopełniacz}}</strong> jest {{wpisz:Administrator danych}}.</p></li>" +
            "<li><p>Dane są przetwarzane w celu przygotowania i prowadzenia formacji oraz dokumentowania jej przebiegu.</p></li>" +
            "<li><p>Dane nie są przekazywane poza struktury Kościoła, z wyjątkiem dostawców usług technicznych (poczta e-mail, hosting).</p></li>" +
            "<li><p>Przysługuje {{rodzaj:Panu|Pani}} prawo dostępu do danych, ich sprostowania oraz ograniczenia przetwarzania.</p></li></ol>" +
            "<p></p><p>{{rodzaj:Zapoznałem|Zapoznałam}} się z klauzulą: ……………………………… (data i podpis)</p>" + Draft),
    };

    /// <summary>Oznaczenie wzoru startowego, którego nikt jeszcze nie zmienił (wtedy można go odświeżyć nową wersją).</summary>
    public const string StarterAuthor = "wzór startowy";

    public static IEnumerable<LetterTemplate> Create(DateTime now) =>
        Bodies.Select((item, index) => new LetterTemplate
        {
            Id = Guid.NewGuid(), Name = DocumentTemplates.Title(item.Type), Body = item.Body, LegacyType = item.Type,
            UseLetterhead = true, IsActive = true, SortOrder = index,
            CreatedAtUtc = now, UpdatedAtUtc = now, UpdatedByEmail = StarterAuthor
        });

    public static Letterhead DefaultLetterhead() => new()
    {
        Id = Letterhead.SingletonId,
        HeaderText = "Diecezjalny Ośrodek Katechumenalny\nSzkoła Katechistów św. Pawła\nDiecezja Bydgoska",
        Place = "Bydgoszcz"
    };

    /// <summary>
    /// Przy pierwszym użyciu zakłada nagłówek domyślny i wzory startowe. Znacznikiem jest wiersz nagłówka,
    /// więc usunięte później wzory startowe nie wracają.
    /// </summary>
    public static async Task EnsureAsync(AppDbContext db, DateTime now, CancellationToken ct)
    {
        if (await db.Letterheads.AnyAsync(ct))
        {
            await RefreshUntouchedAsync(db, ct);
            return;
        }
        db.Letterheads.Add(DefaultLetterhead());
        if (!await db.LetterTemplates.AnyAsync(ct)) db.LetterTemplates.AddRange(Create(now));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Inny proces zdążył założyć wzory równolegle – wystarczy ich wersja.
            db.ChangeTracker.Clear();
        }
    }

    /// <summary>Wzory startowe, których nikt nie edytował, dostają nowszą treść (np. z odmianą imion).</summary>
    private static async Task RefreshUntouchedAsync(AppDbContext db, CancellationToken ct)
    {
        var bodies = Bodies.ToDictionary(b => b.Type, b => b.Body);
        var stale = await db.LetterTemplates.Where(t => t.UpdatedByEmail == StarterAuthor && t.LegacyType != null).ToListAsync(ct);
        var changed = false;
        foreach (var template in stale)
        {
            if (bodies.TryGetValue(template.LegacyType!.Value, out var body) && template.Body != body)
            {
                template.Body = body;
                changed = true;
            }
        }
        if (changed) await db.SaveChangesAsync(ct);
    }
}
