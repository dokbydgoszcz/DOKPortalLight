using DokPortal.Domain.Enums;

namespace DokPortal.Application.People;

public class PersonFunctionDto
{
    public required Guid Id { get; init; }
    public required FunctionType Type { get; init; }
    public Guid? ParishId { get; init; }
    public string? ParishName { get; init; }
    public DateOnly? InstitutedOn { get; init; }
    public string? Notes { get; init; }
}
