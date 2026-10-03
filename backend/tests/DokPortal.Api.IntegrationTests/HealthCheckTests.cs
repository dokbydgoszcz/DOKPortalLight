using System.Net;
using DokPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DokPortal.Api.IntegrationTests;

public class HealthCheckTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public HealthCheckTests(CustomWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_ReturnsOk_WhenDatabaseIsReachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class HealthReadyUnhealthyTests : IClassFixture<HealthReadyUnhealthyTests.UnreachableDatabaseFactory>
{
    private readonly UnreachableDatabaseFactory _factory;

    public HealthReadyUnhealthyTests(UnreachableDatabaseFactory factory) => _factory = factory;

    [Fact]
    public async Task Ready_ReturnsServiceUnavailable_WhenDatabaseIsUnreachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Health_StillReturnsOk_WhenDatabaseIsUnreachable()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Standardowa fabryka, ale baza wskazuje na nieistniejący plik (tylko do odczytu).</summary>
    public class UnreachableDatabaseFactory : CustomWebApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                foreach (var descriptor in services
                             .Where(d => d.ServiceType == typeof(DbContextOptions<AppDbContext>))
                             .ToList())
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlite("Data Source=/nieistniejacy-katalog-dokportal/x.db;Mode=ReadOnly"));
            });
        }
    }
}
