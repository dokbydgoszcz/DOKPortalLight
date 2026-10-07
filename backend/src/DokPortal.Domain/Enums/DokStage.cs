namespace DokPortal.Domain.Enums;

/// <summary>
/// Etapy formacji podopiecznych DOK. Które etapy są dostępne, zależy od ścieżki (zob. <c>DokStages</c>).
/// Wartości liczbowe są zapisane w bazie – nie zmieniać istniejących; Graduate (3) pochodzi z dawnego modelu.
/// </summary>
public enum DokStage
{
    Graduate = 3,
    Evangelization = 4,
    CloserFormation = 5,
    Prekatechumenate = 6,
    Catechumenate = 7,
    Election = 8,
    Neophyte = 9
}
