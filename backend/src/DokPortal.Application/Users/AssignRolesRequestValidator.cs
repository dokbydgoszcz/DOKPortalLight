using FluentValidation;

namespace DokPortal.Application.Users;

public class AssignRolesRequestValidator : AbstractValidator<AssignRolesRequest>
{
    public AssignRolesRequestValidator()
    {
        RuleForEach(x => x.Roles).NotEmpty();
    }
}
