using DokPortal.Domain.Enums;

namespace DokPortal.Application.DokCases;

public class CreateDokCaseRequest
{
    public required Guid PersonId { get; init; }
    public required DokPath Path { get; init; }
    public required DokStage Stage { get; init; }
    public required Guid CatechistPersonId { get; init; }
    public Guid? MentorPersonId { get; init; }
}
