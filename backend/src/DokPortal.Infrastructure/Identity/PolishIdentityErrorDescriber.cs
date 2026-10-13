using Microsoft.AspNetCore.Identity;

namespace DokPortal.Infrastructure.Identity;

/// <summary>Komunikaty Identity po polsku (trafiają do użytkownika w oknach haseł i kont).</summary>
public class PolishIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError PasswordMismatch() => new() { Code = nameof(PasswordMismatch), Description = "Obecne hasło jest nieprawidłowe." };
    public override IdentityError InvalidToken() => new() { Code = nameof(InvalidToken), Description = "Kod jest nieprawidłowy albo wygasł." };
    public override IdentityError PasswordTooShort(int length) => new() { Code = nameof(PasswordTooShort), Description = $"Hasło musi mieć co najmniej {length} znaków." };
    public override IdentityError PasswordRequiresDigit() => new() { Code = nameof(PasswordRequiresDigit), Description = "Hasło musi zawierać cyfrę." };
    public override IdentityError PasswordRequiresLower() => new() { Code = nameof(PasswordRequiresLower), Description = "Hasło musi zawierać małą literę." };
    public override IdentityError PasswordRequiresUpper() => new() { Code = nameof(PasswordRequiresUpper), Description = "Hasło musi zawierać wielką literę." };
    public override IdentityError PasswordRequiresNonAlphanumeric() => new() { Code = nameof(PasswordRequiresNonAlphanumeric), Description = "Hasło musi zawierać znak specjalny (np. ! lub #)." };
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => new() { Code = nameof(PasswordRequiresUniqueChars), Description = $"Hasło musi mieć co najmniej {uniqueChars} różne znaki." };
    public override IdentityError DuplicateEmail(string email) => new() { Code = nameof(DuplicateEmail), Description = $"Adres {email} jest już loginem innego konta." };
    public override IdentityError DuplicateUserName(string userName) => new() { Code = nameof(DuplicateUserName), Description = $"Login {userName} jest już zajęty." };
    public override IdentityError InvalidEmail(string? email) => new() { Code = nameof(InvalidEmail), Description = $"Adres {email} jest nieprawidłowy." };
}
