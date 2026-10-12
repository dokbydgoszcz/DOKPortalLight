namespace DokPortal.Application.Common;

/// <summary>Bezpieczne parametry stronicowania: strona od 1, rozmiar od 1 do limitu (bez 500 przy page=0 i bez „wszystkiego naraz”).</summary>
public static class Paging
{
    public const int DefaultMax = 1000;

    public static (int Page, int PageSize) Normalize(int page, int pageSize, int max = DefaultMax) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, max));
}
