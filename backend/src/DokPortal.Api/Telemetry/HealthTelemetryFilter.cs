using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace DokPortal.Api.Telemetry;

/// <summary>Odrzuca telemetrię żądań health checków (/health, /health/ready), żeby nie zaśmiecała Application Insights.</summary>
public class HealthTelemetryFilter : ITelemetryProcessor
{
    private readonly ITelemetryProcessor _next;

    public HealthTelemetryFilter(ITelemetryProcessor next) => _next = next;

    public void Process(ITelemetry item)
    {
        if (item is RequestTelemetry { Url: not null } request && IsHealthPath(request.Url.AbsolutePath))
        {
            return;
        }

        _next.Process(item);
    }

    private static bool IsHealthPath(string path) =>
        path.Equals("/health", StringComparison.OrdinalIgnoreCase)
        || path.StartsWith("/health/", StringComparison.OrdinalIgnoreCase);
}
