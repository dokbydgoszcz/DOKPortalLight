using DokPortal.Application.Mailing;

namespace DokPortal.Api.Mailing;

/// <summary>
/// Wysyła kolejkę kampanii w tle: po kliknięciu „Wyślij”, przy starcie aplikacji i z codziennego workflow.
/// Działa jeden przebieg naraz; gdy po przebiegu zostają wiadomości (limit dobowy), próbuje ponownie za godzinę.
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

    public MailingQueueRunner(IServiceScopeFactory scopes, ILogger<MailingQueueRunner> logger, IConfiguration configuration)
    {
        _scopes = scopes;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>Odstęp ponownej próby, gdy w kolejce coś zostało.</summary>
    public TimeSpan RetryAfter { get; init; } = TimeSpan.FromHours(1);

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
            try
            {
                using var scope = _scopes.CreateScope();
                var result = await scope.ServiceProvider.GetRequiredService<IMailingService>().ProcessQueueAsync(_stopping.Token);
                pending = result.Pending;
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
        if (_configuration.GetValue("Mailing:ResumeOnStartup", true))
        {
            lock (_gate) _retry = _disposed ? null : new Timer(_ => Kick(), null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
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
