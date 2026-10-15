namespace DokPortal.Application.Parishes;

public class ParishDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? City { get; init; }
    public string? Deanery { get; init; }
    public string? Address { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public Guid? PastorPersonId { get; init; }
    public string? PastorFullName { get; init; }
}
