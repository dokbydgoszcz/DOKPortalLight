using DokPortal.Application.Mailing;
using Microsoft.Extensions.DependencyInjection;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Kolejka mailingu wysyłana od razu (testy nie czekają na wątek w tle).</summary>
public sealed class InlineMailingQueue : IMailingQueue
{
    private readonly IServiceScopeFactory _scopes;

    public InlineMailingQueue(IServiceScopeFactory scopes) => _scopes = scopes;

    public int Kicks { get; private set; }

    public void Kick()
    {
        Kicks++;
        using var scope = _scopes.CreateScope();
        scope.ServiceProvider.GetRequiredService<IMailingService>().ProcessQueueAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
