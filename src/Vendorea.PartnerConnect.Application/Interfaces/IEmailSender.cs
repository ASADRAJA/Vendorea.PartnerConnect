namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Sends transactional email (activation links, password resets, notifications). Implementations must
/// degrade gracefully: a mail-server failure should be logged, never thrown out of the calling request
/// — onboarding a user shouldn't 500 just because SMTP is momentarily unavailable.
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email. The HTML body is required; the plain-text body is optional (a multipart
    /// alternative when supplied). Never throws for transport/SMTP errors — those are logged.
    /// </summary>
    Task SendAsync(string to, string subject, string htmlBody, string? textBody = null, CancellationToken ct = default);
}
