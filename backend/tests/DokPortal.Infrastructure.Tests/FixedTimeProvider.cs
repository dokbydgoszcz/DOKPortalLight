namespace DokPortal.Infrastructure.Tests;

/// <summary>Zegar do testów zależnych od daty (np. awans roku formacji 1 września).</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(int year, int month, int day) => _now = new DateTimeOffset(year, month, day, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;
}
