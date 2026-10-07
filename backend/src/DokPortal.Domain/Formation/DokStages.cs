using DokPortal.Domain.Enums;

namespace DokPortal.Domain.Formation;

/// <summary>Etapy formacji dostępne na poszczególnych ścieżkach DOK oraz polskie nazwy ścieżek i etapów.</summary>
public static class DokStages
{
    private static readonly IReadOnlyList<DokStage> Baptism = new[]
    {
        DokStage.Prekatechumenate, DokStage.Catechumenate, DokStage.Election, DokStage.Neophyte, DokStage.Graduate
    };

    private static readonly IReadOnlyList<DokStage> Confirmation = new[] { DokStage.Evangelization, DokStage.Graduate };

    private static readonly IReadOnlyList<DokStage> Eucharist = new[]
    {
        DokStage.Evangelization, DokStage.CloserFormation, DokStage.Graduate
    };

    /// <summary>Etapy ścieżki w kolejności formacji (ostatni to Absolwent).</summary>
    public static IReadOnlyList<DokStage> For(DokPath path) => path switch
    {
        DokPath.BaptismCandidate => Baptism,
        DokPath.Confirmation => Confirmation,
        DokPath.Communion or DokPath.Conversion or DokPath.ReturnToUnity => Eucharist,
        _ => throw new ArgumentOutOfRangeException(nameof(path))
    };

    public static DokStage First(DokPath path) => For(path)[0];

    public static bool IsValid(DokPath path, DokStage stage) => For(path).Contains(stage);

    /// <summary>Wszystkie etapy w kolejności wyświetlania (np. na pulpicie).</summary>
    public static IReadOnlyList<DokStage> All { get; } = new[]
    {
        DokStage.Evangelization, DokStage.CloserFormation, DokStage.Prekatechumenate, DokStage.Catechumenate,
        DokStage.Election, DokStage.Neophyte, DokStage.Graduate
    };

    public static string Label(DokStage stage) => stage switch
    {
        DokStage.Evangelization => "Ewangelizacja",
        DokStage.CloserFormation => "Formacja bliższa",
        DokStage.Prekatechumenate => "Prekatechumenat",
        DokStage.Catechumenate => "Katechumenat",
        DokStage.Election => "Wybranie",
        DokStage.Neophyte => "Neofita",
        DokStage.Graduate => "Absolwent",
        _ => stage.ToString()
    };

    public static string Label(DokPath path) => path switch
    {
        DokPath.BaptismCandidate => "Kandydaci do Chrztu",
        DokPath.Confirmation => "Bierzmowanie",
        DokPath.Communion => "Eucharystia",
        DokPath.Conversion => "Konwersja",
        DokPath.ReturnToUnity => "Powrót do Jedności",
        _ => path.ToString()
    };
}
