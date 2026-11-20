namespace DokPortal.Application.Users;

public class CreateUserRequest
{
    /// <summary>Login. Przy koncie dla osoby z e-mailem pomijany – loginem jest e-mail osoby; dla osoby bez e-maila zostaje też jej e-mailem.</summary>
    public string? Email { get; init; }
    /// <summary>Hasło tymczasowe. Puste = konto bez hasła; użytkownik ustawi je z linku w zaproszeniu.</summary>
    public string? Password { get; init; }
    public Guid? PersonId { get; init; }
    public required IReadOnlyList<string> Roles { get; init; }
}
