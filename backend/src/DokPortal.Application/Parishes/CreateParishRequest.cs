namespace DokPortal.Application.Parishes;

public class CreateParishRequest
{
    public required string Name { get; init; }
    public string? City { get; init; }
}
