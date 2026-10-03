namespace DokPortal.Application.Users;

public class SetUserPersonRequest
{
    /// <summary>Osoba, z którą ma być powiązane konto; null odpina konto od osoby.</summary>
    public Guid? PersonId { get; init; }
}
