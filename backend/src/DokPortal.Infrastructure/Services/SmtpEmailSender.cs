using System.Net;
using System.Net.Mail;
using DokPortal.Application.Common;

namespace DokPortal.Infrastructure.Services;

public class SmtpEmailSender : IEmailSender
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly string _fromEmail;
    private readonly string _fromName;
    private readonly bool _enableSsl;

    public SmtpEmailSender(string host, int port, string username, string password, string fromEmail, string fromName, bool enableSsl)
    {
        _host = host;
        _port = port;
        _username = username;
        _password = password;
        _fromEmail = fromEmail;
        _fromName = fromName;
        _enableSsl = enableSsl;
    }

    public async Task SendAsync(string toEmail, string subject, string body, CancellationToken ct)
    {
        using var client = new SmtpClient(_host, _port)
        {
            Credentials = new NetworkCredential(_username, _password),
            EnableSsl = _enableSsl
        };
        using var message = new MailMessage
        {
            From = new MailAddress(_fromEmail, _fromName),
            Subject = subject,
            Body = body
        };
        message.To.Add(toEmail);
        await client.SendMailAsync(message, ct);
    }
}
