using DokPortal.Application.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace DokPortal.Api.IntegrationTests;

/// <summary>Pliki w pamięci (jak FakeStorageFactory) i e-maile zapisywane razem z nazwami załączników.</summary>
public class MailingFactory : FakeStorageFactory
{
    public RecordingSender Sender { get; } = new();

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

    public class RecordingSender : IEmailSender
    {
        public List<(string To, string Subject, string Body, string[] Files)> Sent { get; } = new();

        public Task SendAsync(string toEmail, string subject, string body, CancellationToken ct) =>
            SendAsync(toEmail, subject, body, Array.Empty<EmailAttachment>(), ct);

        public Task SendAsync(string toEmail, string subject, string body, IReadOnlyList<EmailAttachment> attachments, CancellationToken ct)
        {
            lock (Sent) Sent.Add((toEmail, subject, body, attachments.Select(a => $"{a.FileName}:{a.Content.Length}").ToArray()));
            return Task.CompletedTask;
        }
    }
}
