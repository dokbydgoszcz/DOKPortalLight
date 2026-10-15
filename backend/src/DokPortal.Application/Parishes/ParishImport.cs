namespace DokPortal.Application.Parishes;

/// <summary>Import parafii i proboszczów z arkusza (np. spisu ze strony diecezji).</summary>
public interface IParishImportService
{
    /// <summary>Czyta arkusz i dopasowuje go do bazy; przy <paramref name="apply"/> = false niczego nie zapisuje (podgląd).</summary>
    /// <exception cref="InvalidOperationException">Plik nie jest arkuszem Excel albo brakuje wymaganych kolumn.</exception>
    Task<ParishImportResultDto> RunAsync(Stream xlsx, bool apply, CancellationToken ct);
}

public class ParishImportResultDto
{
    public required bool Applied { get; init; }
    public required int NewParishes { get; init; }
    public required int MatchedParishes { get; init; }
    public required int NewPeople { get; init; }
    public required int ExistingPeople { get; init; }
    public required int PastorsSet { get; init; }
    public required int PastorsUnchanged { get; init; }
    public required int PastorsReplaced { get; init; }
    public required IReadOnlyList<ParishImportRowDto> Rows { get; init; }
}

public class ParishImportRowDto
{
    public required int Row { get; init; }
    public string? Deanery { get; init; }
    public required string City { get; init; }
    /// <summary>Nazwa parafii w portalu (dopasowanej albo nowej).</summary>
    public required string ParishName { get; init; }
    public required bool NewParish { get; init; }
    public required string PastorFullName { get; init; }
    public required bool NewPerson { get; init; }
    /// <summary>„set” – nowe przypisanie, „unchanged” – już był proboszczem, „replaced” – zastępuje innego.</summary>
    public required string PastorChange { get; init; }
    public string? PreviousPastor { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
}
