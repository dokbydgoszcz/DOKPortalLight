using FluentValidation;

namespace DokPortal.Application.People;

public abstract class PersonRequestValidatorBase<T> : AbstractValidator<T> where T : CreatePersonRequest
{
    protected PersonRequestValidatorBase()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).EmailAddress().MaximumLength(256).When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.NameDayMonth).InclusiveBetween(1, 12).When(x => x.NameDayMonth.HasValue);
        RuleFor(x => x.NameDayDay).InclusiveBetween(1, 31).When(x => x.NameDayDay.HasValue);
        RuleForEach(x => x.Functions).ChildRules(f =>
        {
            f.RuleFor(i => i.Type).IsInEnum();
            f.RuleFor(i => i.Notes).MaximumLength(500);
        });
    }
}

public class CreatePersonRequestValidator : PersonRequestValidatorBase<CreatePersonRequest>
{
}

public class UpdatePersonRequestValidator : PersonRequestValidatorBase<UpdatePersonRequest>
{
}
