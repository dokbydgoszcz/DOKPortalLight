using FluentValidation;

namespace DokPortal.Application.Resources;

public class CreateResourceRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
}

public class UpdateResourceRequest : CreateResourceRequest
{
}

public abstract class ResourceRequestValidatorBase<T> : AbstractValidator<T> where T : CreateResourceRequest
{
    protected ResourceRequestValidatorBase()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(1000);
    }
}

public class CreateResourceRequestValidator : ResourceRequestValidatorBase<CreateResourceRequest>
{
}

public class UpdateResourceRequestValidator : ResourceRequestValidatorBase<UpdateResourceRequest>
{
}
