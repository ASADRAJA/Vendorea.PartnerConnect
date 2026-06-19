namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Toggles for simulating the SPR EDI feedback loop without a live SPR connection. Both default to
/// OFF, so production / live-SPR behaviour is unaffected unless explicitly enabled in config under
/// the "SprSimulation" section.
/// </summary>
public class SprSimulationOptions
{
    public const string SectionName = "SprSimulation";

    /// <summary>
    /// When true, the admin inbound-injection endpoint (POST /api/admin/spr/inbound) is enabled,
    /// letting you post POACK/ASN/invoice XML as if it arrived over SFTP. When false (default), the
    /// endpoint is rejected — turn this OFF when testing against the live SPR system.
    /// </summary>
    public bool AllowInboundInjection { get; set; }

    /// <summary>
    /// When true, Merchant360 order-status/shipment/invoice callbacks are NOT delivered over HTTP —
    /// they are short-circuited (marked delivered) so the payloads can be inspected via the outbox
    /// without a live Merchant360. When false (default), callbacks deliver normally. Turn this OFF
    /// when testing against the live SPR/Merchant360 systems.
    /// </summary>
    public bool CaptureCallbacks { get; set; }
}
