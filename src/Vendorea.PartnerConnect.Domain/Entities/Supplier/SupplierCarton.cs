namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// A carton/package within a shipment.
/// </summary>
public class SupplierCarton
{
    public int Id { get; set; }

    /// <summary>
    /// Parent manifest ID.
    /// </summary>
    public int SupplierShipmentManifestId { get; set; }

    /// <summary>
    /// Carton sequence number within the manifest.
    /// </summary>
    public int CartonNumber { get; set; }

    /// <summary>
    /// SSCC-18 barcode (Serial Shipping Container Code).
    /// </summary>
    public string? Sscc18 { get; set; }

    /// <summary>
    /// Carton tracking number.
    /// </summary>
    public string? TrackingNumber { get; set; }

    /// <summary>
    /// Carton type (CTN, PLT, etc.).
    /// </summary>
    public string? PackageType { get; set; }

    /// <summary>
    /// Carton weight.
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// Weight unit of measure (LB, KG).
    /// </summary>
    public string? WeightUom { get; set; }

    /// <summary>
    /// Carton dimensions - length.
    /// </summary>
    public decimal? Length { get; set; }

    /// <summary>
    /// Carton dimensions - width.
    /// </summary>
    public decimal? Width { get; set; }

    /// <summary>
    /// Carton dimensions - height.
    /// </summary>
    public decimal? Height { get; set; }

    /// <summary>
    /// Dimension unit of measure (IN, CM).
    /// </summary>
    public string? DimensionUom { get; set; }

    /// <summary>
    /// Number of items in this carton.
    /// </summary>
    public int ItemCount { get; set; }

    // Navigation
    public SupplierShipmentManifest? Manifest { get; set; }
    public ICollection<SupplierCartonItem> Items { get; set; } = new List<SupplierCartonItem>();
}
