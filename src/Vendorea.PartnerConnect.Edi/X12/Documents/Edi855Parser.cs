using Vendorea.PartnerConnect.Edi.X12.Models;
using Vendorea.PartnerConnect.Edi.X12.Parser;

namespace Vendorea.PartnerConnect.Edi.X12.Documents;

/// <summary>
/// Parser for EDI 855 Purchase Order Acknowledgment documents.
/// </summary>
public class Edi855Parser
{
    private readonly X12Parser _parser;

    public Edi855Parser()
    {
        _parser = new X12Parser();
    }

    /// <summary>
    /// Parses an EDI 855 document into a PO Acknowledgment model.
    /// </summary>
    public Edi855ParseResult Parse(string ediContent)
    {
        var parseResult = _parser.Parse(ediContent);

        if (!parseResult.Success || parseResult.Envelope == null)
        {
            return new Edi855ParseResult
            {
                Success = false,
                ErrorMessage = parseResult.ErrorMessage ?? "Failed to parse EDI document"
            };
        }

        var acknowledgments = new List<PurchaseOrderAcknowledgment>();
        var errors = new List<string>();

        foreach (var group in parseResult.Envelope.FunctionalGroups)
        {
            foreach (var transactionSet in group.TransactionSets)
            {
                if (transactionSet.TransactionSetCode != "855")
                {
                    continue;
                }

                try
                {
                    var ack = ParseAcknowledgment(transactionSet, parseResult.Envelope);
                    acknowledgments.Add(ack);
                }
                catch (Exception ex)
                {
                    errors.Add($"Error parsing transaction set {transactionSet.ControlNumber}: {ex.Message}");
                }
            }
        }

        return new Edi855ParseResult
        {
            Success = errors.Count == 0,
            Acknowledgments = acknowledgments,
            Errors = errors,
            ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
        };
    }

    private PurchaseOrderAcknowledgment ParseAcknowledgment(
        X12TransactionSet transactionSet,
        X12Envelope envelope)
    {
        var ack = new PurchaseOrderAcknowledgment
        {
            SenderId = envelope.SenderId.Trim(),
            ReceiverId = envelope.ReceiverId.Trim(),
            LineItems = new List<AcknowledgmentLineItem>()
        };

        var lineNumber = 0;

        foreach (var segment in transactionSet.Segments)
        {
            switch (segment.SegmentId)
            {
                case "BAK":
                    // Beginning segment for PO Acknowledgment
                    ack.AcknowledgmentType = segment.GetElement(1);
                    ack.PurchaseOrderNumber = segment.GetElement(2);
                    ack.AcknowledgmentDate = segment.GetElementAsDate(3) ?? DateTime.UtcNow;
                    ack.RequestReferenceNumber = segment.GetElement(4);
                    break;

                case "REF":
                    // Reference identification
                    var refQualifier = segment.GetElement(1);
                    var refValue = segment.GetElement(2);
                    if (refQualifier == "VN")
                    {
                        ack.VendorOrderNumber = refValue;
                    }
                    break;

                case "DTM":
                    // Date/Time reference
                    var dtmQualifier = segment.GetElement(1);
                    var dtmDate = segment.GetElementAsDate(2);
                    if (dtmQualifier == "002" && dtmDate.HasValue)
                    {
                        ack.EstimatedDeliveryDate = dtmDate;
                    }
                    else if (dtmQualifier == "010" && dtmDate.HasValue)
                    {
                        ack.EstimatedShipDate = dtmDate;
                    }
                    break;

                case "PO1":
                    // Line item
                    lineNumber++;
                    var lineItem = new AcknowledgmentLineItem
                    {
                        LineNumber = lineNumber,
                        QuantityOrdered = segment.GetElementAsInt(2),
                        UnitOfMeasure = segment.GetElement(3),
                        UnitPrice = segment.GetElementAsDecimal(4)
                    };

                    // Parse product identifiers
                    for (int i = 6; i <= segment.Elements.Count && i + 1 <= segment.Elements.Count; i += 2)
                    {
                        var qualifier = segment.GetElement(i);
                        var value = segment.GetElement(i + 1);

                        switch (qualifier)
                        {
                            case "SK":
                                lineItem.PartnerSku = value;
                                break;
                            case "UP":
                                lineItem.Upc = value;
                                break;
                            case "VP":
                            case "MG":
                                lineItem.ManufacturerPartNumber = value;
                                break;
                        }
                    }

                    ack.LineItems.Add(lineItem);
                    break;

                case "ACK":
                    // Line item acknowledgment
                    if (ack.LineItems.Count > 0)
                    {
                        var lastItem = ack.LineItems.Last();
                        lastItem.AcknowledgmentCode = segment.GetElement(1);
                        lastItem.QuantityAcknowledged = segment.GetElementAsInt(2);
                        lastItem.AcknowledgmentUnitOfMeasure = segment.GetElement(3);

                        var ackDtmQualifier = segment.GetElement(4);
                        var ackDate = segment.GetElementAsDate(5);
                        if (ackDtmQualifier == "068" && ackDate.HasValue)
                        {
                            lastItem.PromisedShipDate = ackDate;
                        }
                    }
                    break;

                case "CTT":
                    // Transaction totals
                    ack.TotalLineItems = segment.GetElementAsInt(1);
                    break;
            }
        }

        return ack;
    }
}

/// <summary>
/// Result of EDI 855 parsing.
/// </summary>
public class Edi855ParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<PurchaseOrderAcknowledgment> Acknowledgments { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Purchase Order Acknowledgment model.
/// </summary>
public class PurchaseOrderAcknowledgment
{
    public string SenderId { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string AcknowledgmentType { get; set; } = string.Empty;
    public string PurchaseOrderNumber { get; set; } = string.Empty;
    public DateTime AcknowledgmentDate { get; set; }
    public string? RequestReferenceNumber { get; set; }
    public string? VendorOrderNumber { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public DateTime? EstimatedShipDate { get; set; }
    public int TotalLineItems { get; set; }
    public List<AcknowledgmentLineItem> LineItems { get; set; } = new();
}

/// <summary>
/// Line item in a PO acknowledgment.
/// </summary>
public class AcknowledgmentLineItem
{
    public int LineNumber { get; set; }
    public string? PartnerSku { get; set; }
    public string? Upc { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public int QuantityOrdered { get; set; }
    public int QuantityAcknowledged { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    public string? AcknowledgmentUnitOfMeasure { get; set; }
    public decimal UnitPrice { get; set; }
    public string? AcknowledgmentCode { get; set; }
    public DateTime? PromisedShipDate { get; set; }
}
