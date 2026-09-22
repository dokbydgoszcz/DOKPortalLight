namespace DokPortal.Domain.Entities;

public class Person
{
    public Guid Id { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? BirthDate { get; set; }
    public Guid? ParishId { get; set; }
    public Parish? Parish { get; set; }
    public string? Notes { get; set; }
    public int? NameDayMonth { get; set; }
    public int? NameDayDay { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public string FullName => $"{FirstName} {LastName}";
}
