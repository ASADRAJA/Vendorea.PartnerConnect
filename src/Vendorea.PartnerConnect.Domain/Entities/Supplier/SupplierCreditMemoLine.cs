namespace Vendorea.PartnerConnect.Domain.Entities.Supplier;

/// <summary>
/// Line item on a supplier credit memo.
/// </summary>
public class SupplierCreditMemoLine
{
    public int Id { get; set; }

    /// <summary>
    /// Parent credit memo ID.
    /// </summary>
    public int SupplierCreditMemoId { get; set; }

    /// <summary>
    /// Link to the original invoice line if matched.
    /// </summary>
    public int? SupplierInvoiceLineId { get; set; }

    /// <summary>
    /// Line number.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Supplier's SKU/item ID.
    /// </summary>
    public string SupplierSku { get; set; } = string.Empty;

    /// <summary>
    /// Customer's SKU reference.
    /// </summary>
    public string? CustomerSku { get; set; }

    /// <summary>
    /// UPC/EAN code.
    /// </summary>
    public string? Upc { get; set; }

    /// <summary>
    /// Item description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Quantity credited/returned.
    /// </summary>
    public int QuantityCredited { get; set; }

    /// <summary>
    /// Unit of measure.
    /// </summary>
    public string UnitOfMeasure { get; set; } = "EA";

    /// <summary>
    /// Unit price.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Extended credit amount.
    /// </summary>
    public decimal ExtendedCredit { get; set; }

    /// <summary>
    /// Tax credit for this line.
    /// </summary>
    public decimal? TaxCredit { get; set; }

    /// <summary>
    /// Total line credit.
    /// </summary>
    public decimal LineTotal { get; set; }

    /// <summary>
    /// Reason for this line credit.
    /// </summary>
    public CreditMemoReason? LineReason { get; set; }

    /// <summary>
    /// Notes for this line.
    /// </summary>
    public string? Notes { get; set; }

    // Navigation
    public SupplierCreditMemo? CreditMemo { get; set; }
    public SupplierInvoiceLine? OriginalInvoiceLine { get; set; }
}
