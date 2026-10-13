using DokPortal.Domain.Enums;
using FluentValidation;

namespace DokPortal.Application.Supervisions;

public class CreateSupervisionRequest
{
    public required Institution Institution { get; init; }
    public required string GroupLabel { get; init; }
    public required DateOnly SupervisionDate { get; init; }
    public int? AttendeesCount { get; init; }
    public int? ExpectedCount { get; init; }
    public string? Topic { get; init; }
    public string? Conclusion { get; init; }
    /// <summary>Imienna lista obecności; gdy podana, zastępuje poprzednią i wyznacza liczby obecnych/oczekiwanych. Brak = bez zmian.</summary>
    public IReadOnlyList<SupervisionAttendanceItem>? Attendance { get; init; }
}

public class SupervisionAttendanceItem
{
    public required Guid PersonId { get; init; }
    public bool IsPresent { get; init; }
}

public class CreateSupervisionRequestValidator : AbstractValidator<CreateSupervisionRequest>
{
    public CreateSupervisionRequestValidator()
    {
        RuleFor(x => x.Institution).IsInEnum();
        RuleFor(x => x.GroupLabel).Must(g => !string.IsNullOrWhiteSpace(g)).WithMessage("Podaj grupę superwizyjną.").MaximumLength(200);
        RuleFor(x => x.Attendance!.Count).LessThanOrEqualTo(500).When(x => x.Attendance is not null);
    }
}

/// <summary>Grupa superwizyjna z misji (pole „Grupa superwizyjna”) i jej katechiści z ważną misją.</summary>
public class SupervisionGroupDto
{
    public required string Name { get; init; }
    public required IReadOnlyList<SupervisionGroupMemberDto> Members { get; init; }
}

public class SupervisionGroupMemberDto
{
    public required Guid PersonId { get; init; }
    public required string FullName { get; init; }
}
