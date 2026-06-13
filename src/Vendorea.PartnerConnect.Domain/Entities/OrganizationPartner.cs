namespace Vendorea.PartnerConnect.Domain.Entities;

/// <summary>
/// A trading partner an organization selected at registration — i.e., a partner that the
/// organization's tenants are allowed to connect to. Partner availability is granted at the
/// org level; the actual technical connection and per-tenant accounts are handled elsewhere.
/// </summary>
public class OrganizationPartner
{
    public int Id { get; set; }

    /// <summary>
    /// The organization that selected the partner.
    /// </summary>
    public int OrganizationId { get; set; }

    /// <summary>
    /// The selected trading partner.
    /// </summary>
    public int TradingPartnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Organization? Organization { get; set; }
    public TradingPartner? TradingPartner { get; set; }
}
