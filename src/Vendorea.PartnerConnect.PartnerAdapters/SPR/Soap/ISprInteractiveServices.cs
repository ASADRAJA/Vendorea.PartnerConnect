namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

/// <summary>
/// Interface for SPR interactive SOAP services.
/// Provides real-time queries for status, inventory, and tracking.
///
/// NOTE: This is for interactive queries ONLY. Order submission and other
/// transactional documents use the document pipeline, not SOAP.
/// </summary>
public interface ISprInteractiveServices
{
    /// <summary>
    /// Gets the status of an order from SPR via real-time SOAP query.
    /// </summary>
    /// <param name="poNumber">The purchase order number.</param>
    /// <param name="config">SPR SOAP configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Current order status from SPR.</returns>
    Task<SprOrderStatusResult> GetOrderStatusAsync(
        string poNumber,
        SprSoapConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets real-time inventory availability for items via SOAP query.
    /// </summary>
    /// <param name="skus">List of SKUs to check.</param>
    /// <param name="config">SPR SOAP configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Inventory availability for the requested SKUs.</returns>
    Task<SprInventoryResult> GetInventoryAsync(
        IEnumerable<string> skus,
        SprSoapConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tracking information for a shipment via SOAP query.
    /// </summary>
    /// <param name="trackingNumber">The tracking number to look up.</param>
    /// <param name="config">SPR SOAP configuration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Tracking details from SPR.</returns>
    Task<SprTrackingResult> GetTrackingAsync(
        string trackingNumber,
        SprSoapConfig config,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests the connection to SPR SOAP web services.
    /// </summary>
    /// <param name="config">SPR configuration to test.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Connection test result.</returns>
    Task<SprConnectionTestResult> TestConnectionAsync(
        SprSoapConfig config,
        CancellationToken cancellationToken = default);
}
