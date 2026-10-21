using DokPortal.Api.Startup;
using DokPortal.Application.Mailing;

namespace DokPortal.Api.Mailing;

/// <summary>
/// Wysyła kolejkę kampanii w tle: po kliknięciu „Wyślij”, przy starcie aplikacji i z codziennego workflow.
/// Działa jeden przebieg naraz; gdy po przebiegu zostają wiadomości (limit dobowy), próbuje ponownie za godzinę.
/// Na Azure start aplikacji wznawia kolejkę tylko wtedy, gdy znacznik mówi, że coś w niej zostało – inaczej każdy
/// restart budziłby bazę (darmowy limit liczy czas, gdy baza jest włączona).
/// </summary>
public sealed class MailingQueueRunner : IMailingQueue, IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<MailingQueueRunner> _logger;
    private readonly IConfiguration _configuration;
    private readonly CancellationTokenSource _stopping = new();
    private readonly object _gate = new();
    private bool _running;
    private bool _again;
    private bool _disposed;
    private Timer? _retry;
    private readonly LocalStateFiles? _state;

    /// <summary>Znacznik „w kolejce może coś czekać” – ustawiany na czas przebiegu, kasowany, gdy kolejka jest pusta.</summary>
    public const string PendingFlag = "mailing-pending.flag";

    public MailingQueueRunner(IServiceScopeFactory scopes, ILogger<MailingQueueRunner> logger, IConfiguration configuration, LocalStateFiles? state = null)
    {
        _state = state;
        _scopes = scopes;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>Odstęp ponownej próby, gdy w kolejce coś zostało.</summary>
    public TimeSpan RetryAfter { get; init; } = TimeSpan.FromHours(1);

    /// <summary>Opóźnienie wznowienia kolejki po starcie aplikacji.</summary>
    public TimeSpan StartupDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>Ostatni przebieg (do testów i diagnostyki).</summary>
    public Task Current { get; private set; } = Task.CompletedTask;

    public void Kick()
    {
        lock (_gate)
        {
            if (_disposed) return;
            if (_running)
            {
                _again = true;
                return;
            }
            _running = true;
            Current = Task.Run(RunAsync);
        }
    }

    private async Task RunAsync()
    {
        while (true)
        {
            var pending = 0;
            _state?.Write(PendingFlag, DateTime.UtcNow.ToString("O"));
            try
            {
                using var scope = _scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IMailingService>().ProcessQueueAsync(_stopping.Token);
                pending = result.Pending;
                if (pending == 0) _state?.Delete(PendingFlag);
                if (result.Sent + result.Failed > 0)
                    _logger.LogInformation("Kolejka mailingu: wysłano {Sent}, błędy {Failed}, czeka {Pending} (limit {Limit}/dobę)",
                        result.Sent, result.Failed, result.Pending, result.DailyLimit);
            }
            catch (OperationCanceledException) when (_stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Kolejka mailingu: przebieg nie powiódł się");
            }

            lock (_gate)
            {
                if (_again)
                {
                    _again = false;
                    continue;
                }
                _running = false;
                if (pending > 0 && !_disposed)
                {
                    _retry?.Dispose();
                    _retry = new Timer(_ => Kick(), null, RetryAfter, Timeout.InfiniteTimeSpan);
                }
                return;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Po restarcie (np. uśpienie darmowego planu) dokończ przerwane kampanie.
        var mayHaveWork = _state is not { Enabled: true } || _state.Exists(PendingFlag);
        if (_configuration.GetValue("Mailing:ResumeOnStartup", true) && mayHaveWork)
        {
            lock (_gate) _retry = _disposed ? null : new Timer(_ => Kick(), null, StartupDelay, Timeout.InfiniteTimeSpan);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Shutdown();
        return Task.CompletedTask;
    }

    /// <summary>Zatrzymuje przebiegi i ponowienia; bezpieczne przy wielokrotnym wywołaniu i w dowolnej kolejności z <see cref="Dispose"/>.</summary>
    private void Shutdown()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _retry?.Dispose();
            _retry = null;
        }
        try
        {
            _stopping.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    // Źródła anulowania nie zwalniamy: przebieg w tle może jeszcze sprawdzać jego token.
    public void Dispose() => Shutdown();
}
