namespace DokPortal.Application.People;

public record DuplicateMatch(Guid Id, string FullName, string MatchedOn);

/// <summary>
/// Kod "EmailTaken": adres e-mail jest już zajęty (twarda blokada – e-mail bywa loginem).
/// Kod "PhoneDuplicate": telefon ma już inna osoba (ostrzeżenie, można zapisać z ConfirmDuplicate).
/// </summary>
public class DuplicatePersonException : Exception
{
    public string Code { get; }
    public IReadOnlyList<DuplicateMatch> Matches { get; }

    public DuplicatePersonException(string code, string message, IReadOnlyList<DuplicateMatch> matches) : base(message)
    {
        Code = code;
        Matches = matches;
    }
}
