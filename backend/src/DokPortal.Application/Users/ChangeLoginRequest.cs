using FluentValidation;

namespace DokPortal.Application.Users;

/// <summary>Nowy login (e-mail) konta, np. poprawka literówki.</summary>
public class ChangeLoginRequest
{
    public required string Email { get; init; }
}

public class ChangeLoginRequestValidator : AbstractValidator<ChangeLoginRequest>
{
    public ChangeLoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().WithMessage("Podaj login (adres e-mail).")
            .EmailAddress().WithMessage("Login musi być poprawnym adresem e-mail.").MaximumLength(256);
    }
}
