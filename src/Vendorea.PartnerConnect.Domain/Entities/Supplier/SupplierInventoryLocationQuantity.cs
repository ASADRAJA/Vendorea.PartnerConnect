namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Inventory quantity at a specific warehouse/location.
/// </summary>
public class SupplierInventoryLocationQuantity
{
    public int Id { get; set; }

    /// <summary>
    /// Parent inventory item ID.
    /// </summary>
    public int SupplierInventoryItemId { get; set; }

    /// <summary>
    /// Warehouse/location code.
    /// </summary>
    public string LocationCode { get; set; } = string.Empty;

    /// <summary>
    /// Location name/description.
    /// </summary>
    public string? LocationName { get; set; }

    /// <summary>
    /// Location city.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// Location state/province.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// Location country.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Quantity available at this location.
    /// </summary>
    public int QuantityAvailable { get; set; }

    /// <summary>
    /// Quantity on hand at this location.
    /// </summary>
    public int? QuantityOnHand { get; set; }

    /// <summary>
    /// Quantity allocated at this location.
    /// </summary>
    public int? QuantityAllocated { get; set; }

    /// <summary>
    /// Estimated ship date from this location.
    /// </summary>
    public DateTime? EstimatedShipDate { get; set; }

    /// <summary>
    /// Transit time in days from this location.
    /// </summary>
    public int? TransitDays { get; set; }

    // Navigation
    public SupplierInventoryItem? InventoryItem { get; set; }
}
