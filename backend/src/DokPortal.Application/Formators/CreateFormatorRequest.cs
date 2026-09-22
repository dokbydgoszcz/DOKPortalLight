namespace DokPortal.Application.Formators;

public class CreateFormatorRequest
{
    public required Guid PersonId { get; init; }
    public required string Function { get; init; }
}
