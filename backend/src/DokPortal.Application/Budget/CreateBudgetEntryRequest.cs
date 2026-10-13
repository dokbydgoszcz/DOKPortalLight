using DokPortal.Domain.Enums;
using FluentValidation;

namespace DokPortal.Application.Budget;

public class CreateBudgetEntryRequest
{
    public required BudgetFund Fund { get; init; }
    public required DateOnly EntryDate { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required BudgetEntryType Type { get; init; }
    public required decimal Amount { get; init; }
}

/// <summary>Poprawka wpisu; funduszu (SKŚP/DOK) zmienić nie można.</summary>
public class UpdateBudgetEntryRequest
{
    public required DateOnly EntryDate { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required BudgetEntryType Type { get; init; }
    public required decimal Amount { get; init; }
}

public class CreateBudgetEntryRequestValidator : AbstractValidator<CreateBudgetEntryRequest>
{
    public CreateBudgetEntryRequestValidator()
    {
        RuleFor(x => x.Fund).IsInEnum();
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Podaj opis operacji.").MaximumLength(500);
        RuleFor(x => x.Category).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Podaj kategorię.").MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage(BudgetMessages.PositiveAmount).LessThan(100_000_000);
    }
}

public class UpdateBudgetEntryRequestValidator : AbstractValidator<UpdateBudgetEntryRequest>
{
    public UpdateBudgetEntryRequestValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Description).Must(d => !string.IsNullOrWhiteSpace(d)).WithMessage("Podaj opis operacji.").MaximumLength(500);
        RuleFor(x => x.Category).Must(c => !string.IsNullOrWhiteSpace(c)).WithMessage("Podaj kategorię.").MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage(BudgetMessages.PositiveAmount).LessThan(100_000_000);
    }
}

internal static class BudgetMessages
{
    public const string PositiveAmount = "Kwota musi być większa od zera (typ mówi, czy to przychód, czy wydatek).";
}
