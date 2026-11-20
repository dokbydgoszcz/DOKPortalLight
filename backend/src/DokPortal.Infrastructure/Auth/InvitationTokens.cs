namespace DokPortal.Infrastructure.Auth;

/// <summary>
/// Kod z e-maila „Zaproszenie do DOK Portal” (dostawca: <c>DokPortal.Api.Security.InvitationTokenProvider</c>, 7 dni).
/// Zawiera znacznik bezpieczeństwa konta, więc ustawienie hasła unieważnia go – link działa jeden raz.
/// </summary>
public static class InvitationTokens
{
    public const string ProviderName = "DokPortalInvitation";
    public const string Purpose = "SetPassword";
}
