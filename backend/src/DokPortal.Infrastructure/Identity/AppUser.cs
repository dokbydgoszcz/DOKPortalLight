using Microsoft.AspNetCore.Identity;

namespace DokPortal.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public Guid? PersonId { get; set; }
    /// <summary>Konto wyłączone przez administratora (np. katechista odszedł): nie zaloguje się, a otwarte sesje wygasają.</summary>
    public bool IsDeactivated { get; set; }
}
