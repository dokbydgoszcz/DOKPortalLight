using DokPortal.Api.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Znaczniki na dysku App Service: migracje raz na wersję, a restart nie budzi bazy bez potrzeby.</summary>
public class StartupStateTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "dokportal-state-" + Guid.NewGuid().ToString("N"));

    private LocalStateFiles State(string? dir = null) =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Startup:StateDirectory"] = dir ?? _dir }).Build());

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    [Fact]
    public void Markers_AreWrittenReadAndDeleted_AndOffOutsideAzure()
    {
        var state = State();
        Assert.True(state.Enabled);
        Assert.Null(state.Read("a"));
        state.Write("a", " x \n");
        Assert.Equal("x", state.Read("a"));
        Assert.True(state.Exists("a"));
        state.Delete("a");
        Assert.False(state.Exists("a"));
        state.Delete("a");

        var off = new LocalStateFiles(new ConfigurationBuilder().Build());
        Assert.False(off.Enabled);
        off.Write("a", "x");
        Assert.Null(off.Read("a"));
        off.Delete("a");
    }

    [Fact]
    public void BrokenStorage_DoesNotStopTheApp()
    {
        File.WriteAllText(_dir, "to plik, nie katalog");
        try
        {
            var state = new LocalStateFiles(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Startup:StateDirectory"] = Path.Combine(_dir, "pod") }).Build(),
                NullLogger<LocalStateFiles>.Instance);
            state.Write("a", "x");
            Assert.Null(state.Read("a"));
            state.Delete("a");
        }
        finally
        {
            File.Delete(_dir);
        }
    }

    [Fact]
    public async Task Migrations_RunOncePerVersion_AndAgainAfterAFailureOrWhenForced()
    {
        var state = State();
        var runs = 0;
        Task Work() { runs++; return Task.CompletedTask; }

        Assert.True(await StartupDatabase.RunOncePerVersionAsync(state, "v1", false, Work, NullLogger.Instance));
        Assert.False(await StartupDatabase.RunOncePerVersionAsync(state, "v1", false, Work, NullLogger.Instance));
        Assert.True(await StartupDatabase.RunOncePerVersionAsync(state, "v1", true, Work, NullLogger.Instance));
        Assert.Equal(2, runs);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            StartupDatabase.RunOncePerVersionAsync(state, "v2", false, () => throw new InvalidOperationException("baza śpi"), NullLogger.Instance));
        Assert.Equal("v1", state.Read(StartupDatabase.StampFile));
        // Baza niedostępna (np. limit darmowej oferty): aplikacja startuje dalej, a następny start próbuje ponownie.
        Assert.False(await StartupDatabase.RunOncePerVersionAsync(state, "v2", false,
            () => throw new InvalidOperationException("EF", new TimeoutException()), NullLogger.Instance));
        Assert.Equal("v1", state.Read(StartupDatabase.StampFile));
        Assert.True(await StartupDatabase.RunOncePerVersionAsync(state, "v2", false, Work, NullLogger.Instance));
        Assert.Equal(3, runs);

        Assert.Equal(StartupDatabase.CurrentStamp(), StartupDatabase.CurrentStamp());
        Assert.Equal(4, StartupDatabase.CurrentStamp().Split(',').Length);
    }
}
