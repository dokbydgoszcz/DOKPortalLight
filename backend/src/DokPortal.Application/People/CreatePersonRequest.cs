namespace DokPortal.Application.People;

public class CreatePersonRequest
{
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public DateOnly? BirthDate { get; init; }
    public Guid? ParishId { get; init; }
    public string? Notes { get; init; }
    public int? NameDayMonth { get; init; }
    public int? NameDayDay { get; init; }
    /// <summary>Funkcje osoby. Brak listy (null) zostawia funkcje bez zmian; pusta lista je usuwa.</summary>
    public IReadOnlyList<PersonFunctionInput>? Functions { get; init; }
    /// <summary>Potwierdza zapis mimo ostrzeżenia o telefonie, który ma już inna osoba (nie dotyczy e-maila).</summary>
    public bool ConfirmDuplicate { get; init; }
}
