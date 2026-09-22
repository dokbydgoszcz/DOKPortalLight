namespace DokPortal.Application.Parishes;

public class ParishDto
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public string? City { get; init; }
}
