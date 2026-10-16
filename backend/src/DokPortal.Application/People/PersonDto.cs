namespace DokPortal.Application.People;

public class PersonDto
{
    public required Guid Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? AddressStreet { get; init; }
    public string? AddressPostalCode { get; init; }
    public string? AddressCity { get; init; }
    public DateOnly? BirthDate { get; init; }
    public Guid? ParishId { get; init; }
    public string? ParishName { get; init; }
    public string? Notes { get; init; }
    public int? NameDayMonth { get; init; }
    public int? NameDayDay { get; init; }
    public IReadOnlyList<PersonFunctionDto> Functions { get; init; } = Array.Empty<PersonFunctionDto>();
    /// <summary>Kiedy wgrano zdjęcie profilowe (null = brak); służy też do odświeżania podglądu.</summary>
    public DateTime? PhotoUpdatedAtUtc { get; init; }
    /// <summary>Osoba wypisała się z mailingu (null = dostaje kampanie).</summary>
    public DateTime? MailingOptOutAtUtc { get; init; }
    /// <summary>Dane osoby zanonimizowano (RODO).</summary>
    public DateTime? AnonymizedAtUtc { get; init; }
}
