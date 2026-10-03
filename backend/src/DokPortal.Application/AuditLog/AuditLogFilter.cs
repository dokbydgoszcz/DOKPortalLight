using DokPortal.Domain.Enums;

namespace DokPortal.Application.AuditLog;

public class AuditLogFilter
{
    public const int DefaultTake = 500;
    public const int MaxTake = 5000;

    /// <summary>Tekst szukany w e-mailu użytkownika, nazwie akcji i opisie obiektu (bez rozróżniania wielkości liter).</summary>
    public string? Search { get; init; }
    public string? Action { get; init; }
    public AuditResult? Result { get; init; }
    /// <summary>Pierwszy dzień (UTC) włącznie.</summary>
    public DateOnly? From { get; init; }
    /// <summary>Ostatni dzień (UTC) włącznie.</summary>
    public DateOnly? To { get; init; }
    /// <summary>Ile najnowszych wpisów zwrócić (1..MaxTake).</summary>
    public int Take { get; init; } = DefaultTake;
}
