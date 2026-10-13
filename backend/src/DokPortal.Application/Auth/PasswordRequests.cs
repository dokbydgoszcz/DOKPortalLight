using FluentValidation;

namespace DokPortal.Application.Auth;

/// <summary>Zmiana własnego hasła przez zalogowanego użytkownika.</summary>
public class ChangePasswordRequest
{
    public required string CurrentPassword { get; init; }
    public required string NewPassword { get; init; }
}

/// <summary>„Nie pamiętam hasła”: prośba o link do ustawienia nowego hasła.</summary>
public class ForgotPasswordRequest
{
    public required string Email { get; init; }
}

/// <summary>Ustawienie nowego hasła jednorazowym kodem z e-maila.</summary>
public class ResetPasswordWithTokenRequest
{
    public required string UserId { get; init; }
    public required string Token { get; init; }
    public required string NewPassword { get; init; }
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(256);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(256)
            .WithMessage("Nowe hasło musi mieć co najmniej 8 znaków.");
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword)
            .WithMessage("Nowe hasło musi się różnić od obecnego.");
    }
}

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
    }
}

public class ResetPasswordWithTokenRequestValidator : AbstractValidator<ResetPasswordWithTokenRequest>
{
    public ResetPasswordWithTokenRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty().MaximumLength(450);
        RuleFor(x => x.Token).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(8).MaximumLength(256)
            .WithMessage("Nowe hasło musi mieć co najmniej 8 znaków.");
    }
}
