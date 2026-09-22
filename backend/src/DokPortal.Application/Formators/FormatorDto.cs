namespace DokPortal.Application.Formators;

public class FormatorDto
{
    public required Guid Id { get; init; }
    public required Guid PersonId { get; init; }
    public required string PersonFullName { get; init; }
    public string? PersonEmail { get; init; }
    public string? PersonPhone { get; init; }
    public required string Function { get; init; }
}
