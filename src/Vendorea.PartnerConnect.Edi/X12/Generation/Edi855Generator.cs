using Vendorea.PartnerConnect.Edi.X12.Documents;
using Vendorea.PartnerConnect.Edi.X12.Models;
using Vendorea.PartnerConnect.Edi.X12.Parser;

namespace Vendorea.PartnerConnect.Edi.X12.Generation;

/// <summary>
/// Generator for EDI 855 Purchase Order Acknowledgment documents.
/// </summary>
public class Edi855Generator
{
    private readonly X12Tokenizer _tokenizer;

    public Edi855Generator()
    {
        _tokenizer = new X12Tokenizer();
    }

    /// <summary>
    /// Generates an EDI 855 document from a PO Acknowledgment model.
    /// </summary>
    public string Generate(
        PurchaseOrderAcknowledgment acknowledgment,
        Edi855GeneratorOptions options)
    {
        var envelope = new X12Envelope
        {
            SenderId = options.SenderId.PadRight(15),
            ReceiverId = options.ReceiverId.PadRight(15),
            SenderQualifier = options.SenderQualifier,
            ReceiverQualifier = options.ReceiverQualifier,
            InterchangeControlNumber = options.InterchangeControlNumber.PadLeft(9, '0'),
            UsageIndicator = options.IsProduction ? "P" : "T"
        };

        var group = new X12FunctionalGroup
        {
            FunctionalIdentifier = "PR", // Purchase Order Acknowledgment
            SenderCode = options.ApplicationSenderId,
            ReceiverCode = options.ApplicationReceiverId,
            GroupControlNumber = options.GroupControlNumber
        };

        var transactionSet = new X12TransactionSet
        {
            TransactionSetCode = "855",
            ControlNumber = options.TransactionSetControlNumber
        };

        // BAK - Beginning Segment for PO Acknowledgment
        var bak = new X12Segment { SegmentId = "BAK" };
        bak.SetElement(1, "AC"); // Acknowledgment - With Detail and Change
        bak.SetElement(2, acknowledgment.PurchaseOrderNumber);
        bak.SetElement(3, acknowledgment.AcknowledgmentDate.ToString("yyyyMMdd"));
        if (!string.IsNullOrEmpty(acknowledgment.VendorOrderNumber))
        {
            bak.SetElement(4, acknowledgment.VendorOrderNumber);
        }
        transactionSet.Segments.Add(bak);

        // REF - Vendor Order Number
        if (!string.IsNullOrEmpty(acknowledgment.VendorOrderNumber))
        {
            var refVn = new X12Segment { SegmentId = "REF" };
            refVn.SetElement(1, "VN");
            refVn.SetElement(2, acknowledgment.VendorOrderNumber);
            transactionSet.Segments.Add(refVn);
        }

        // DTM - Estimated Ship Date
        if (acknowledgment.EstimatedShipDate.HasValue)
        {
            var dtmShip = new X12Segment { SegmentId = "DTM" };
            dtmShip.SetElement(1, "010"); // Requested Ship Date
            dtmShip.SetElement(2, acknowledgment.EstimatedShipDate.Value.ToString("yyyyMMdd"));
            transactionSet.Segments.Add(dtmShip);
        }

        // DTM - Estimated Delivery Date
        if (acknowledgment.EstimatedDeliveryDate.HasValue)
        {
            var dtmDel = new X12Segment { SegmentId = "DTM" };
            dtmDel.SetElement(1, "002"); // Delivery Requested
            dtmDel.SetElement(2, acknowledgment.EstimatedDeliveryDate.Value.ToString("yyyyMMdd"));
            transactionSet.Segments.Add(dtmDel);
        }

        // Line Items
        foreach (var lineItem in acknowledgment.LineItems)
        {
            // PO1 - Baseline Item Data
            var po1 = new X12Segment { SegmentId = "PO1" };
            po1.SetElement(1, lineItem.LineNumber.ToString());
            po1.SetElement(2, lineItem.QuantityOrdered.ToString());
            po1.SetElement(3, lineItem.UnitOfMeasure);
            po1.SetElement(4, lineItem.UnitPrice.ToString("F2"));

            var elementPos = 6;
            if (!string.IsNullOrEmpty(lineItem.PartnerSku))
            {
                po1.SetElement(elementPos++, "SK");
                po1.SetElement(elementPos++, lineItem.PartnerSku);
            }
            if (!string.IsNullOrEmpty(lineItem.Upc))
            {
                po1.SetElement(elementPos++, "UP");
                po1.SetElement(elementPos++, lineItem.Upc);
            }
            if (!string.IsNullOrEmpty(lineItem.ManufacturerPartNumber))
            {
                po1.SetElement(elementPos++, "VP");
                po1.SetElement(elementPos++, lineItem.ManufacturerPartNumber);
            }
            transactionSet.Segments.Add(po1);

            // ACK - Line Item Acknowledgment
            var ack = new X12Segment { SegmentId = "ACK" };
            ack.SetElement(1, lineItem.AcknowledgmentCode ?? "IA"); // Item Accepted
            ack.SetElement(2, lineItem.QuantityAcknowledged.ToString());
            ack.SetElement(3, lineItem.AcknowledgmentUnitOfMeasure ?? lineItem.UnitOfMeasure);

            if (lineItem.PromisedShipDate.HasValue)
            {
                ack.SetElement(4, "068"); // Ship Date
                ack.SetElement(5, lineItem.PromisedShipDate.Value.ToString("yyyyMMdd"));
            }
            transactionSet.Segments.Add(ack);
        }

        // CTT - Transaction Totals
        var ctt = new X12Segment { SegmentId = "CTT" };
        ctt.SetElement(1, acknowledgment.LineItems.Count.ToString());
        transactionSet.Segments.Add(ctt);

        group.TransactionSets.Add(transactionSet);
        envelope.FunctionalGroups.Add(group);

        return _tokenizer.Build(envelope);
    }
}

/// <summary>
/// Options for generating EDI 855 documents.
/// </summary>
public class Edi855GeneratorOptions
{
    public string SenderId { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string SenderQualifier { get; set; } = "ZZ";
    public string ReceiverQualifier { get; set; } = "ZZ";
    public string ApplicationSenderId { get; set; } = string.Empty;
    public string ApplicationReceiverId { get; set; } = string.Empty;
    public string InterchangeControlNumber { get; set; } = "1";
    public string GroupControlNumber { get; set; } = "1";
    public string TransactionSetControlNumber { get; set; } = "0001";
    public bool IsProduction { get; set; } = true;
}
