namespace DokPortal.Application.Users;

/// <summary>Kto zmienia konta: tylko administrator może nadać rolę Administrator; nikt nie wyłączy własnego konta.</summary>
public record UserActor(string UserId, bool IsAdministrator);
