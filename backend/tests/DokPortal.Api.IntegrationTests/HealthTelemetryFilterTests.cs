using DokPortal.Api.Telemetry;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class HealthTelemetryFilterTests
{
    private class RecordingProcessor : ITelemetryProcessor
    {
        public List<ITelemetry> Received { get; } = new();
        public void Process(ITelemetry item) => Received.Add(item);
    }

    private static (HealthTelemetryFilter Filter, RecordingProcessor Next) Create()
    {
        var next = new RecordingProcessor();
        return (new HealthTelemetryFilter(next), next);
    }

    private static RequestTelemetry Request(string path) => new() { Url = new Uri("https://dokportal-api.azurewebsites.net" + path) };

    [Theory]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/HEALTH")]
    public void Process_DropsHealthRequests(string path)
    {
        var (filter, next) = Create();

        filter.Process(Request(path));

        Assert.Empty(next.Received);
    }

    [Theory]
    [InlineData("/api/people")]
    [InlineData("/api/healthy-things")]
    public void Process_PassesOtherRequests(string path)
    {
        var (filter, next) = Create();
        var item = Request(path);

        filter.Process(item);

        Assert.Single(next.Received);
        Assert.Same(item, next.Received[0]);
    }

    [Fact]
    public void Process_PassesNonRequestTelemetryAndRequestsWithoutUrl()
    {
        var (filter, next) = Create();

        filter.Process(new TraceTelemetry("komunikat"));
        filter.Process(new RequestTelemetry());

        Assert.Equal(2, next.Received.Count);
    }
}

public class TelemetryEnabledStartupTests : IClassFixture<TelemetryEnabledStartupTests.TelemetryEnabledFactory>
{
    private readonly TelemetryEnabledFactory _factory;

    public TelemetryEnabledStartupTests(TelemetryEnabledFactory factory) => _factory = factory;

    [Fact]
    public async Task App_StartsAndServesRequests_WhenApplicationInsightsConnectionStringIsSet()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(_factory.Services.GetService(typeof(Microsoft.ApplicationInsights.TelemetryClient)));
    }

    [Fact]
    public void TelemetryIsNotRegistered_WithoutConnectionString()
    {
        using var plain = new CustomWebApplicationFactory();

        Assert.Null(plain.Services.GetService(typeof(Microsoft.ApplicationInsights.TelemetryClient)));
    }

    /// <summary>Standardowa fabryka z ustawionym (fałszywym) connection stringiem Application Insights.</summary>
    public class TelemetryEnabledFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("APPLICATIONINSIGHTS_CONNECTION_STRING", "InstrumentationKey=00000000-0000-0000-0000-000000000000");
        }
    }
}
