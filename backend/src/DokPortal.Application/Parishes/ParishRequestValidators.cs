using FluentValidation;

namespace DokPortal.Application.Parishes;

public abstract class ParishRequestValidatorBase<T> : AbstractValidator<T> where T : CreateParishRequest
{
    protected ParishRequestValidatorBase()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(200);
        RuleFor(x => x.Deanery).MaximumLength(100);
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.Phone).MaximumLength(100);
        RuleFor(x => x.Email).MaximumLength(256);
        RuleFor(x => x.Website).MaximumLength(300);
    }
}

public class CreateParishRequestValidator : ParishRequestValidatorBase<CreateParishRequest>
{
}

public class UpdateParishRequestValidator : ParishRequestValidatorBase<UpdateParishRequest>
{
}
