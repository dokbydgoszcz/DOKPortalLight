using FluentValidation;

namespace DokPortal.Application.Parishes;

public abstract class ParishRequestValidatorBase<T> : AbstractValidator<T> where T : CreateParishRequest
{
    protected ParishRequestValidatorBase()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.City).MaximumLength(200);
    }
}

public class CreateParishRequestValidator : ParishRequestValidatorBase<CreateParishRequest>
{
}

public class UpdateParishRequestValidator : ParishRequestValidatorBase<UpdateParishRequest>
{
}
