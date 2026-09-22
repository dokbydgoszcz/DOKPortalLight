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
}
