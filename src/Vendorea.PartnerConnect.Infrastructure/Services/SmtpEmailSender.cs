using System.Net.Mail;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vendorea.PartnerConnect.Application.Interfaces;

namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <summary>
/// <see cref="IEmailSender"/> over <see cref="System.Net.Mail.SmtpClient"/> — no external package. It
/// is deliberately non-blocking on failure: any SMTP error is logged (never rethrown) so onboarding a
/// user is never broken by a flaky/absent mail server. In Development the full message body — including
/// activation links — is also logged at Information level, so flows are testable with no SMTP running.
/// </summary>
public class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(IOptions<EmailOptions> options, IHostEnvironment environment, ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default)
    {
        // In Development, surface the message (incl. any activation link) so flows can be exercised
        // even when no SMTP sink is running.
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation(
                "[DEV EMAIL] To: {To} | Subject: {Subject}\n{Body}",
                to, subject, textBody ?? htmlBody);
        }

        try
        {
            using var message = new MailMessage
            {
                From = new MailAddress(_options.From, _options.FromName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };
            message.To.Add(to);

            // Attach a plain-text alternative when supplied (better deliverability / accessibility).
            if (!string.IsNullOrWhiteSpace(textBody))
            {
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                    textBody, null, "text/plain"));
                message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                    htmlBody, null, "text/html"));
            }

            using var client = new SmtpClient(_options.Host, _options.Port)
            {
                EnableSsl = _options.UseSsl
            };

            await client.SendMailAsync(message, ct);
            _logger.LogInformation("Sent email to {To} (subject: {Subject})", to, subject);
        }
        catch (Exception ex)
        {
            // Graceful degradation: log and move on. The caller's request must not fail on a mail error.
            _logger.LogError(ex,
                "Failed to send email to {To} (subject: {Subject}) via {Host}:{Port}. Continuing.",
                to, subject, _options.Host, _options.Port);
        }
    }
}
