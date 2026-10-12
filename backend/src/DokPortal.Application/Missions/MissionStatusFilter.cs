namespace DokPortal.Application.Missions;

/// <summary>Filtr listy misji: wszystkie, aktualne (nie wygasły) albo wygasłe.</summary>
public enum MissionStatusFilter
{
    All,
    Current,
    Expired
}
