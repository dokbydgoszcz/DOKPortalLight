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
        RuleFor(x => x.PersonIds).NotEmpty().WithMessage("Wybierz co najmniej jedną osobę na liście odbiorców.")
            .When(x => x.Group == DokPortal.Domain.Enums.MailingGroup.Custom);
        RuleFor(x => x.PersonIds!.Count).LessThanOrEqualTo(2000).WithMessage("Lista może mieć najwyżej 2000 osób.")
            .When(x => x.PersonIds is not null);
    }
}
