namespace DokPortal.Domain.Entities;

public class Parish
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public string? City { get; set; }
}
