using DokPortal.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Jak zwykła fabryka, ale e-maile trafiają do listy w pamięci zamiast „niezkonfigurowanego” nadawcy.</summary>
public class RecordingEmailFactory : CustomWebApplicationFactory
{
    public RecordingEmailSender Sender { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IEmailSender)).ToList())
            {
                services.Remove(descriptor);
            }
            services.AddSingleton<IEmailSender>(Sender);
        });
    }

    public class RecordingEmailSender : IEmailSender
    {
        public List<(string To, string Subject)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
        {
            lock (Sent) Sent.Add((toEmail, subject));
            return Task.CompletedTask;
        }
    }
}
