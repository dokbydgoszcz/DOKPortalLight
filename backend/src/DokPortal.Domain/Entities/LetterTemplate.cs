using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Entities;

/// <summary>Wzór pisma edytowany w portalu: treść (prosty HTML z edytora) z polami {{…}}.</summary>
public class LetterTemplate
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    /// <summary>Treść z edytora (akapity, nagłówki, pogrubienia, listy, wyrównanie) z polami {{osoba.imię}}, {{wpisz:…}}.</summary>
    public required string Body { get; set; }
    /// <summary>Drukuj wspólny nagłówek i stopkę (papier firmowy).</summary>
    public bool UseLetterhead { get; set; } = true;
    /// <summary>Wyłączony wzór nie jest proponowany przy generowaniu (zostaje w historii).</summary>
    public bool IsActive { get; set; } = true;
    /// <summary>Dawny wbudowany typ pisma, z którego powstał ten wzór (dla historii i starych linków).</summary>
    public DocumentTemplate? LegacyType { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public string? UpdatedByEmail { get; set; }
}

/// <summary>Wspólny nagłówek i stopka pism (jeden wiersz w bazie).</summary>
public class Letterhead
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;
    /// <summary>Tekst obok logo, np. nazwa i adres ośrodka (wiersze rozdzielone znakiem nowej linii).</summary>
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    /// <summary>Miejscowość wystawienia pisma, np. „Bydgoszcz” – pole {{pismo.miejscowość}}.</summary>
    public string? Place { get; set; }
    public string? LogoBlobPath { get; set; }
    public string? LogoContentType { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
