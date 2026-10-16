using DokPortal.Domain.Enums;

namespace DokPortal.Application.Documents;

public class GenerateDocumentRequest
{
    /// <summary>Wzór pisma z portalu.</summary>
    public Guid? LetterTemplateId { get; init; }
    /// <summary>Dawny typ pisma – zamieniany na wzór startowy tego typu (zgodność ze starszymi wywołaniami).</summary>
    public DocumentTemplate? Template { get; init; }
    public Guid PersonId { get; init; }
    /// <summary>Uwagi – trafiają do historii i do pola {{pismo.uwagi}}.</summary>
    public string? AdditionalNotes { get; init; }
    /// <summary>Wartości pól {{wpisz:…}}.</summary>
    public Dictionary<string, string>? Inputs { get; init; }
    /// <summary>„pdf” (domyślnie) albo „docx”.</summary>
    public string? Format { get; init; }
}
