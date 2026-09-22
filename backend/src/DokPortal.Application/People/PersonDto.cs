namespace DokPortal.Application.People;

public class PersonDto
{
    public required Guid Id { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required string FullName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public DateOnly? BirthDate { get; init; }
    public Guid? ParishId { get; init; }
    public string? ParishName { get; init; }
    public string? Notes { get; init; }
    public int? NameDayMonth { get; init; }
    public int? NameDayDay { get; init; }
}
