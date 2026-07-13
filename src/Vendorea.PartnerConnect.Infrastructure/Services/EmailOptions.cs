namespace Vendorea.PartnerConnect.Infrastructure.Services;

/// <summary>
/// SMTP configuration bound from the <c>Email</c> config section. Dev defaults target a local SMTP
/// sink. NOTE: smtp4dev / Mailhog typically listen for SMTP on 25 or 1025 and expose their web UI on
/// 3000/5000 — so confirm your sink's actual SMTP port. All values are overridable via config.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>SMTP host. Dev default: localhost.</summary>
    public string Host { get; set; } = "localhost";

    /// <summary>SMTP port. Dev default: 3000 (the user's dev SMTP; adjust to your sink's SMTP port).</summary>
    public int Port { get; set; } = 3000;

    /// <summary>From address on outbound mail.</summary>
    public string From { get; set; } = "no-reply@partnerconnect.local";

    /// <summary>Friendly From display name.</summary>
    public string FromName { get; set; } = "PartnerConnect";

    /// <summary>Whether to negotiate TLS (leave false for local dev sinks).</summary>
    public bool UseSsl { get; set; }
}
