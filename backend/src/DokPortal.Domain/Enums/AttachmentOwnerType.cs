namespace DokPortal.Domain.Enums;

/// <summary>Do jakiego rodzaju rekordu dołączono plik (załączniki nie mają osobnego klucza obcego na każdy typ).</summary>
public enum AttachmentOwnerType
{
    PastoralNote,
    Supervision,
    Mission,
    /// <summary>Plik z globalnej biblioteki zasobów dla katechistów.</summary>
    Resource
}
