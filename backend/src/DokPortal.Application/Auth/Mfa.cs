using FluentValidation;

namespace DokPortal.Application.Auth;

/// <summary>Klucz dla aplikacji uwierzytelniającej i link otpauth:// do kodu QR.</summary>
public sealed record MfaSetupResponse(string SharedKey, string OtpAuthUri);

/// <summary>MFA włączone: jednorazowe kody zapasowe (pokazywane tylko teraz) i nowa sesja.</summary>
public sealed record MfaEnabledResponse(IReadOnlyList<string> RecoveryCodes, LoginResponse Session);

public sealed record MfaStatusResponse(bool Enabled, int RecoveryCodesLeft);

/// <summary>Hasło poprawne, ale konto ma MFA – trzeba podać kod z aplikacji (krok 2 logowania).</summary>
public sealed record MfaChallengeResponse(bool MfaRequired, string MfaToken);

public class MfaCodeRequest
{
    public required string Code { get; init; }
}

public class MfaVerifyRequest
{
    public required string MfaToken { get; init; }
    /// <summary>6 cyfr z aplikacji albo kod zapasowy.</summary>
    public required string Code { get; init; }
}

public class MfaCodeRequestValidator : AbstractValidator<MfaCodeRequest>
{
    public MfaCodeRequestValidator() => RuleFor(x => x.Code).NotEmpty().WithMessage("Podaj kod z aplikacji.").MaximumLength(20);
}

public class MfaVerifyRequestValidator : AbstractValidator<MfaVerifyRequest>
{
    public MfaVerifyRequestValidator()
    {
        RuleFor(x => x.MfaToken).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Code).NotEmpty().WithMessage("Podaj kod z aplikacji.").MaximumLength(20);
    }
}
