namespace DokPortal.Domain.Entities;

public class Person : ISoftDeletable
{
    public Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    /// <summary>Adres zamieszkania (do pism): ulica z numerem.</summary>
    public string? AddressStreet { get; set; }
    public string? AddressPostalCode { get; set; }
    public string? AddressCity { get; set; }
    /// <summary>Płeć (do odmiany w pismach); puste = rozpoznawana z imienia.</summary>
    public DokPortal.Domain.Enums.PersonSex? Sex { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Guid? ParishId { get; set; }
    public Parish? Parish { get; set; }
    public string? Notes { get; set; }
    public int? NameDayMonth { get; set; }
    public int? NameDayDay { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }
    public string? DeletedBy { get; set; }
    public List<PersonFunction> Functions { get; set; } = new();

    public string FullName => $"{FirstName} {LastName}";
    /// <summary>Zdjęcie profilowe w magazynie plików (karta osoby); puste = brak zdjęcia.</summary>
    public string? PhotoBlobPath { get; set; }
    public string? PhotoContentType { get; set; }
    public DateTime? PhotoUpdatedAtUtc { get; set; }
    /// <summary>Osoba wypisała się z mailingu (link w stopce albo na prośbę) – nie dostaje kampanii.</summary>
    public DateTime? MailingOptOutAtUtc { get; set; }
    /// <summary>Tajny klucz linku „wypisz się” w stopce kampanii (tworzony przy pierwszej wysyłce).</summary>
    public string? MailingUnsubscribeToken { get; set; }
    /// <summary>Dane osoby usunięto na jej żądanie (RODO) – zostały tylko powiązania statystyczne.</summary>
    public DateTime? AnonymizedAtUtc { get; set; }
}
