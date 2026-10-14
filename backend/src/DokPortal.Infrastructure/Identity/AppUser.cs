using Microsoft.AspNetCore.Identity;

namespace DokPortal.Infrastructure.Identity;

public class AppUser : IdentityUser
{
    public Guid? PersonId { get; set; }
    /// <summary>Konto wyłączone przez administratora (np. katechista odszedł): nie zaloguje się, a otwarte sesje wygasają.</summary>
    public bool IsDeactivated { get; set; }
    /// <summary>Hasło nadał administrator (nowe konto albo reset) – użytkownik musi je zmienić po zalogowaniu.</summary>
    public bool MustChangePassword { get; set; }
    /// <summary>Tajny klucz prywatnego adresu kalendarza (iCal) do subskrypcji w Google/Outlook; puste = wyłączony.</summary>
    public string? CalendarFeedToken { get; set; }
    /// <summary>Licznik logowań bez weryfikacji dwuetapowej – co trzecie logowanie portal przypomina o włączeniu MFA.</summary>
    public int LoginsWithoutMfa { get; set; }
}
