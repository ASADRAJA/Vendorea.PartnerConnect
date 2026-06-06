namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// An item within an inventory snapshot.
/// Represents a specific SKU's inventory across locations.
/// </summary>
public class SupplierInventoryItem
{
    public int Id { get; set; }

    /// <summary>
    /// Parent snapshot ID.
    /// </summary>
    public int SupplierInventorySnapshotId { get; set; }

    /// <summary>
    /// Supplier's SKU/item ID.
    /// </summary>
    public string SupplierSku { get; set; } = string.Empty;

    /// <summary>
    /// UPC/EAN code.
    /// </summary>
    public string? Upc { get; set; }

    /// <summary>
    /// Manufacturer part number.
    /// </summary>
    public string? ManufacturerPartNumber { get; set; }

    /// <summary>
    /// Item description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Total quantity available across all locations.
    /// </summary>
    public int QuantityAvailable { get; set; }

    /// <summary>
    /// Quantity on hand (may include allocated).
    /// </summary>
    public int? QuantityOnHand { get; set; }

    /// <summary>
    /// Quantity allocated/reserved.
    /// </summary>
    public int? QuantityAllocated { get; set; }

    /// <summary>
    /// Quantity on order/in transit.
    /// </summary>
    public int? QuantityOnOrder { get; set; }

    /// <summary>
    /// Quantity backordered.
    /// </summary>
    public int? QuantityBackordered { get; set; }

    /// <summary>
    /// Unit of measure.
    /// </summary>
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Current cost/price.
    /// </summary>
    public decimal? UnitCost { get; set; }

    /// <summary>
    /// List price.
    /// </summary>
    public decimal? ListPrice { get; set; }

    /// <summary>
    /// Item status from supplier.
    /// </summary>
    public InventoryItemStatus Status { get; set; } = InventoryItemStatus.Available;

    /// <summary>
    /// Status reason/note.
    /// </summary>
    public string? StatusReason { get; set; }

    /// <summary>
    /// Expected availability date if backordered.
    /// </summary>
    public DateTime? ExpectedAvailabilityDate { get; set; }

    /// <summary>
    /// Lead time in days.
    /// </summary>
    public int? LeadTimeDays { get; set; }

    /// <summary>
    /// Minimum order quantity.
    /// </summary>
    public int? MinimumOrderQuantity { get; set; }

    /// <summary>
    /// Order multiple/pack quantity.
    /// </summary>
    public int? OrderMultiple { get; set; }

    /// <summary>
    /// Whether this item is discontinued.
    /// </summary>
    public bool IsDiscontinued { get; set; }

    /// <summary>
    /// Whether this item is hazmat.
    /// </summary>
    public bool IsHazmat { get; set; }

    /// <summary>
    /// Weight per unit.
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// Weight unit of measure.
    /// </summary>
    public string? WeightUom { get; set; }

    // Navigation
    public SupplierInventorySnapshot? Snapshot { get; set; }
    public ICollection<SupplierInventoryLocationQuantity> LocationQuantities { get; set; } = new List<SupplierInventoryLocationQuantity>();
}

/// <summary>
/// Status of an inventory item.
/// </summary>
public enum InventoryItemStatus
{
    /// <summary>Item is available for order.</summary>
    Available = 0,

    /// <summary>Item is in stock but limited quantity.</summary>
    LimitedStock = 10,

    /// <summary>Item is temporarily out of stock.</summary>
    OutOfStock = 20,

    /// <summary>Item is on backorder.</summary>
    Backordered = 30,

    /// <summary>Item is discontinued.</summary>
    Discontinued = 40,

    /// <summary>Item is unavailable.</summary>
    Unavailable = 50
}
