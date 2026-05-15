using System.Net;
using System.Net.Mail;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LiveFuelMap.Infrastructure.Email;

public sealed class NoopEmailSender(ILogger<NoopEmailSender> logger) : IEmailSender
{
    public Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Email queued to {To}: {Subject}. IsHtml={IsHtml}. Body: {Body}", to, subject, isHtml, body);
        return Task.CompletedTask;
    }
}

public sealed class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendEmailAsync(string to, string subject, string body, bool isHtml = false, CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("Email:Smtp");
        var host = section["Host"];
        if (string.IsNullOrWhiteSpace(host))
        {
            logger.LogWarning("SMTP host is not configured. Email to {To} skipped.", to);
            return;
        }

        using var message = new MailMessage(section["From"] ?? "noreply@livefuelmap.local", to, subject, body)
        {
            IsBodyHtml = isHtml
        };
        using var client = new SmtpClient(host, section.GetValue("Port", 587))
        {
            EnableSsl = section.GetValue("UseSsl", true),
            Credentials = new NetworkCredential(section["User"], section["Password"])
        };

        await client.SendMailAsync(message, cancellationToken);
    }
}
