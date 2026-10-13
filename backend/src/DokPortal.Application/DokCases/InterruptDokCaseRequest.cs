using FluentValidation;

namespace DokPortal.Application.DokCases;

/// <summary>Przerwanie formacji: od kiedy i dlaczego (np. rezygnacja, przeprowadzka, choroba).</summary>
public class InterruptDokCaseRequest
{
    public required DateOnly Date { get; init; }
    public required string Reason { get; init; }
}

public class InterruptDokCaseRequestValidator : AbstractValidator<InterruptDokCaseRequest>
{
    public InterruptDokCaseRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Podaj powód przerwania.").MaximumLength(500);
    }
}

/// <summary>Które sprawy pokazać: tylko w toku, tylko przerwane; brak = wszystkie.</summary>
public enum DokCaseStatusFilter
{
    Active = 1,
    Interrupted = 2
}
