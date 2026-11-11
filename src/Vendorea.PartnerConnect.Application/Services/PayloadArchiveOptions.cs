namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Retention and capture settings for the provider payload archive.
/// </summary>
/// <remarks>
/// **Every window here is provisional.** The approved contract records retention as an open
/// decision that depends on Amazon's data policies for the roles actually granted, and says it
/// should be set deliberately rather than defaulted. These values are therefore chosen to be
/// defensible and short, not authoritative — they exist so that nothing is ever stored without an
/// expiry, and they are expected to be replaced by a policy decision.
/// </remarks>
public class PayloadArchiveOptions
{
    public const string SectionName = "Integrations:PayloadArchive";

    /// <summary>Whether payloads are captured at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long a captured payload is kept. Provisional.
    /// </summary>
    /// <remarks>
    /// Thirty days answers "what did Amazon send for this order?" for as long as anyone is still
    /// asking, which in practice is days rather than months. It is also the growth control: this
    /// is the one Phase 3 component whose size is open-ended, and the retention window is what
    /// closes it.
    /// </remarks>
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// Payloads larger than this are not captured.
    /// </summary>
    /// <remarks>
    /// A guard against one pathological response filling the table. A redacted order runs a few
    /// kilobytes; something at 256 KB is not an order and is better noted as skipped than stored.
    /// </remarks>
    public int MaxPayloadBytes { get; set; } = 256 * 1024;

    /// <summary>Rows deleted per purge pass, so a backlog cannot hold a long transaction open.</summary>
    public int PurgeBatchSize { get; set; } = 1000;
}

/// <summary>
/// Retention for buyer data held on the sales order itself.
/// </summary>
/// <remarks>
/// Separate from the archive because the archive never holds personal data — this does. The order
/// row carries buyer identity and street address because a merchant-fulfilled order cannot be
/// shipped without them, which is exactly why it needs a window.
///
/// **Both windows are provisional**, pending the same policy decision.
/// </remarks>
public class BuyerDataRetentionOptions
{
    public const string SectionName = "Integrations:BuyerDataRetention";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How long after purchase a settled order keeps its buyer data.
    /// </summary>
    /// <remarks>
    /// Applied only to orders in a terminal state. Redacting an order still awaiting shipment
    /// would destroy the address it needs to ship to — correct privacy behaviour producing an
    /// undeliverable order.
    /// </remarks>
    public TimeSpan SettledOrderWindow { get; set; } = TimeSpan.FromDays(90);

    /// <summary>
    /// The outer limit, applied regardless of status.
    /// </summary>
    /// <remarks>
    /// Without this, an order stuck outside a terminal state would hold personal data for ever,
    /// and the retention policy would be conditional on a state machine always completing. An
    /// order this old that has not settled is itself a problem, so reaching this limit is logged
    /// as a warning rather than passing quietly.
    /// </remarks>
    public TimeSpan HardLimit { get; set; } = TimeSpan.FromDays(180);

    /// <summary>Orders redacted per sweep.</summary>
    public int BatchSize { get; set; } = 500;
}
