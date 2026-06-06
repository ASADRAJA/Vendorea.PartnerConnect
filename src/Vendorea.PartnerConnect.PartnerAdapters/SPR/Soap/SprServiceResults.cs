namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

/// <summary>
/// Result of order status inquiry via SOAP.
/// </summary>
public class SprOrderStatusResult
{
    public bool Success { get; set; }
    public string? PoNumber { get; set; }
    public string? PartnerOrderNumber { get; set; }
    public string? OrderStatus { get; set; }
    public DateTime? OrderDate { get; set; }
    public DateTime? ExpectedShipDate { get; set; }
    public DateTime? ActualShipDate { get; set; }
    public string? TrackingNumber { get; set; }
    public List<SprOrderLineStatus> Lines { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Line-level order status from SOAP inquiry.
/// </summary>
public class SprOrderLineStatus
{
    public int LineNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public int QuantityOrdered { get; set; }
    public int QuantityShipped { get; set; }
    public int QuantityBackordered { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Result of real-time inventory availability check via SOAP.
/// </summary>
public class SprInventoryResult
{
    public bool Success { get; set; }
    public List<SprInventoryItem> Items { get; set; } = new();
    public string? ErrorMessage { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Inventory availability for a single item from SOAP inquiry.
/// </summary>
public class SprInventoryItem
{
    public string Sku { get; set; } = string.Empty;
    public int QuantityAvailable { get; set; }
    public int QuantityOnOrder { get; set; }
    public string AvailabilityStatus { get; set; } = string.Empty;
    public DateTime? ExpectedRestockDate { get; set; }
    public string? WarehouseCode { get; set; }
}

/// <summary>
/// Result of tracking inquiry via SOAP.
/// </summary>
public class SprTrackingResult
{
    public bool Success { get; set; }
    public string? TrackingNumber { get; set; }
    public string? Carrier { get; set; }
    public string? ServiceLevel { get; set; }
    public string? CurrentStatus { get; set; }
    public string? CurrentLocation { get; set; }
    public DateTime? ShipDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime? EstimatedDelivery { get; set; }
    public List<SprTrackingEvent> Events { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// A tracking event in the shipment history.
/// </summary>
public class SprTrackingEvent
{
    public DateTime Timestamp { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string? Description { get; set; }
}

/// <summary>
/// Result of connection test via SOAP.
/// </summary>
public class SprConnectionTestResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string? ServerVersion { get; set; }
    public DateTime TestedAt { get; set; } = DateTime.UtcNow;
}
