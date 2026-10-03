using FluentValidation;

namespace DokPortal.Application.Mailing;

public class CreateMailingCampaignRequestValidator : AbstractValidator<CreateMailingCampaignRequest>
{
    public CreateMailingCampaignRequestValidator()
    {
        // 200 znaków to długość kolumny w bazie
        RuleFor(x => x.Subject).NotEmpty().WithMessage("Podaj temat kampanii.")
            .MaximumLength(200).WithMessage("Temat może mieć najwyżej 200 znaków.");
        RuleFor(x => x.Body).NotEmpty().WithMessage("Podaj treść kampanii.");
        RuleFor(x => x.Group).IsInEnum().WithMessage("Wybierz grupę odbiorców.");
    }
}
