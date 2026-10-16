namespace DokPortal.Application.Documents;

public class LetterTemplateDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string Body { get; init; }
    public bool UseLetterhead { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public string? UpdatedByEmail { get; init; }
    /// <summary>Pola {{wpisz:…}} – o nie zapyta formularz generowania.</summary>
    public IReadOnlyList<string> Inputs { get; init; } = Array.Empty<string>();
    /// <summary>Pola, których portal nie zna (prawdopodobnie literówka).</summary>
    public IReadOnlyList<string> UnknownFields { get; init; } = Array.Empty<string>();
    /// <summary>Ile pism wygenerowano z tego wzoru.</summary>
    public int UsageCount { get; init; }
}

public class SaveLetterTemplateRequest
{
    public required string Name { get; init; }
    public required string Body { get; init; }
    public bool UseLetterhead { get; init; } = true;
    public bool IsActive { get; init; } = true;
}

public class LetterFieldDto
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required string Group { get; init; }
    public required string Example { get; init; }
}

public class LetterheadDto
{
    public string? HeaderText { get; init; }
    public string? FooterText { get; init; }
    public string? Place { get; init; }
    public bool HasLogo { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public class SaveLetterheadRequest
{
    public string? HeaderText { get; init; }
    public string? FooterText { get; init; }
    public string? Place { get; init; }
}

/// <summary>Podgląd wzoru (także niezapisanego) – na wybranej osobie albo na przykładowych danych.</summary>
public class PreviewLetterRequest
{
    public required string Body { get; init; }
    public bool UseLetterhead { get; init; } = true;
    public Guid? PersonId { get; init; }
    public Dictionary<string, string>? Inputs { get; init; }
    public string? Format { get; init; }
}

/// <summary>Gotowy plik pisma.</summary>
public class LetterFile
{
    public required byte[] Bytes { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    /// <summary>Pola bez danych (w piśmie są kropki do wypełnienia).</summary>
    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();
}

public interface ILetterTemplateService
{
    Task<IReadOnlyList<LetterTemplateDto>> ListAsync(bool includeInactive, CancellationToken ct);
    Task<LetterTemplateDto?> GetAsync(Guid id, CancellationToken ct);
    Task<LetterTemplateDto> CreateAsync(SaveLetterTemplateRequest request, string? userEmail, CancellationToken ct);
    Task<LetterTemplateDto?> UpdateAsync(Guid id, SaveLetterTemplateRequest request, string? userEmail, CancellationToken ct);
    /// <exception cref="InvalidOperationException">Z wzoru wygenerowano już pisma – można go tylko wyłączyć.</exception>
    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
    IReadOnlyList<LetterFieldDto> Fields();
    Task<LetterheadDto> GetLetterheadAsync(CancellationToken ct);
    Task<LetterheadDto> SaveLetterheadAsync(SaveLetterheadRequest request, CancellationToken ct);
    /// <exception cref="InvalidOperationException">Zły format albo za duży plik.</exception>
    Task<LetterheadDto> SaveLogoAsync(Stream content, string contentType, long length, CancellationToken ct);
    Task<LetterheadDto> DeleteLogoAsync(CancellationToken ct);
    Task<(byte[] Bytes, string ContentType)?> GetLogoAsync(CancellationToken ct);
    Task<LetterFile> PreviewAsync(PreviewLetterRequest request, string userId, CancellationToken ct);
}
