namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Orchestrates dispatching a PartnerConnect order to SPR as an outbound EZPO4 PO:
/// maps the domain order to the canonical model, generates and strictly validates the
/// EZPO4 XML, sends it over SFTP (one PO per file), and advances the order to Processing.
///
/// This is the explicit "transmit to supplier" action. It is intentionally separate from
/// the admin acknowledge (review) step, which does not imply supplier-dispatch-ready.
/// </summary>
public interface ISprOutboundOrderService
{
    /// <summary>
    /// Generates, validates and sends the order to SPR.
    /// </summary>
    /// <param name="force">
    /// Sends an order the status rules would refuse. A resend is not idempotent - the outbound
    /// filename is timestamped, so SPR receives a second file with the same CustomerPONo and
    /// treats it as another purchase order. Only for the case where a file was written but SPR
    /// never answered, and only on a deliberate action by someone who has been told that.
    /// </param>
    Task<SprTransmitResult> TransmitOrderAsync(
        int orderId, bool force = false, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of an outbound PO transmit attempt.
/// </summary>
public class SprTransmitResult
{
    public bool Success { get; set; }

    /// <summary>Order was not found.</summary>
    public bool NotFound { get; set; }

    /// <summary>Order is in a status that cannot be transmitted (e.g. Cancelled/Completed).</summary>
    public bool InvalidState { get; set; }

    /// <summary>Generation or strict XSD validation failed (bad data — order marked Failed).</summary>
    public bool ValidationFailed { get; set; }

    /// <summary>The generated outbound PartnerDocument id, when created.</summary>
    public int? DocumentId { get; set; }

    /// <summary>The remote SFTP path the PO was written to, when sent.</summary>
    public string? RemotePath { get; set; }

    public List<string> Errors { get; set; } = new();
    public string? ErrorMessage { get; set; }

    public static SprTransmitResult NotFoundResult() => new() { NotFound = true, ErrorMessage = "Order not found" };
}
