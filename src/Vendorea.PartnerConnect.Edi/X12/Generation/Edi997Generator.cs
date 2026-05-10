using Vendorea.PartnerConnect.Edi.X12.Documents;
using Vendorea.PartnerConnect.Edi.X12.Models;
using Vendorea.PartnerConnect.Edi.X12.Parser;

namespace Vendorea.PartnerConnect.Edi.X12.Generation;

/// <summary>
/// Generator for EDI 997 Functional Acknowledgment documents.
/// </summary>
public class Edi997Generator
{
    private readonly X12Tokenizer _tokenizer;

    public Edi997Generator()
    {
        _tokenizer = new X12Tokenizer();
    }

    /// <summary>
    /// Generates an EDI 997 acknowledgment for a received X12 envelope.
    /// </summary>
    public string Generate(X12Envelope receivedEnvelope, Edi997GeneratorOptions options)
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
            FunctionalIdentifier = "FA", // Functional Acknowledgment
            SenderCode = options.ApplicationSenderId,
            ReceiverCode = options.ApplicationReceiverId,
            GroupControlNumber = options.GroupControlNumber
        };

        // Generate a 997 for each functional group in the received envelope
        foreach (var receivedGroup in receivedEnvelope.FunctionalGroups)
        {
            var transactionSet = new X12TransactionSet
            {
                TransactionSetCode = "997",
                ControlNumber = options.TransactionSetControlNumber
            };

            // AK1 - Functional Group Response Header
            var ak1 = new X12Segment { SegmentId = "AK1" };
            ak1.SetElement(1, receivedGroup.FunctionalIdentifier);
            ak1.SetElement(2, receivedGroup.GroupControlNumber);
            transactionSet.Segments.Add(ak1);

            var acceptedCount = 0;
            var receivedCount = receivedGroup.TransactionSets.Count;

            // AK2/AK5 for each transaction set
            foreach (var receivedTs in receivedGroup.TransactionSets)
            {
                // AK2 - Transaction Set Response Header
                var ak2 = new X12Segment { SegmentId = "AK2" };
                ak2.SetElement(1, receivedTs.TransactionSetCode);
                ak2.SetElement(2, receivedTs.ControlNumber);
                transactionSet.Segments.Add(ak2);

                // Validate the transaction set (simple validation for now)
                var hasErrors = ValidateTransactionSet(receivedTs, transactionSet);

                // AK5 - Transaction Set Response Trailer
                var ak5 = new X12Segment { SegmentId = "AK5" };
                ak5.SetElement(1, hasErrors ? "R" : "A"); // A=Accepted, R=Rejected
                transactionSet.Segments.Add(ak5);

                if (!hasErrors)
                {
                    acceptedCount++;
                }
            }

            // AK9 - Functional Group Response Trailer
            var ak9 = new X12Segment { SegmentId = "AK9" };
            ak9.SetElement(1, acceptedCount == receivedCount ? "A" : (acceptedCount > 0 ? "E" : "R"));
            ak9.SetElement(2, receivedCount.ToString());
            ak9.SetElement(3, receivedCount.ToString());
            ak9.SetElement(4, acceptedCount.ToString());
            transactionSet.Segments.Add(ak9);

            group.TransactionSets.Add(transactionSet);
        }

        envelope.FunctionalGroups.Add(group);

        return _tokenizer.Build(envelope);
    }

    /// <summary>
    /// Generates an EDI 997 from a functional acknowledgment model.
    /// </summary>
    public string Generate(FunctionalAcknowledgment acknowledgment, Edi997GeneratorOptions options)
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
            FunctionalIdentifier = "FA",
            SenderCode = options.ApplicationSenderId,
            ReceiverCode = options.ApplicationReceiverId,
            GroupControlNumber = options.GroupControlNumber
        };

        var transactionSet = new X12TransactionSet
        {
            TransactionSetCode = "997",
            ControlNumber = options.TransactionSetControlNumber
        };

        // AK1 - Functional Group Response Header
        var ak1 = new X12Segment { SegmentId = "AK1" };
        ak1.SetElement(1, acknowledgment.AcknowledgedFunctionalIdentifier);
        ak1.SetElement(2, acknowledgment.AcknowledgedGroupControlNumber);
        transactionSet.Segments.Add(ak1);

        // Transaction set responses
        foreach (var tsResponse in acknowledgment.TransactionSetResponses)
        {
            // AK2 - Transaction Set Response Header
            var ak2 = new X12Segment { SegmentId = "AK2" };
            ak2.SetElement(1, tsResponse.TransactionSetIdentifierCode);
            ak2.SetElement(2, tsResponse.TransactionSetControlNumber);
            transactionSet.Segments.Add(ak2);

            // Segment errors (AK3/AK4)
            foreach (var segError in tsResponse.SegmentErrors)
            {
                var ak3 = new X12Segment { SegmentId = "AK3" };
                ak3.SetElement(1, segError.SegmentIdCode);
                ak3.SetElement(2, segError.SegmentPositionInTransactionSet.ToString());
                if (!string.IsNullOrEmpty(segError.LoopIdentifierCode))
                {
                    ak3.SetElement(3, segError.LoopIdentifierCode);
                }
                if (!string.IsNullOrEmpty(segError.SegmentSyntaxErrorCode))
                {
                    ak3.SetElement(4, segError.SegmentSyntaxErrorCode);
                }
                transactionSet.Segments.Add(ak3);

                foreach (var elemError in segError.ElementErrors)
                {
                    var ak4 = new X12Segment { SegmentId = "AK4" };
                    ak4.SetElement(1, elemError.ElementPositionInSegment.ToString());
                    if (elemError.ComponentDataElementPositionInComposite.HasValue)
                    {
                        ak4.SetElement(2, elemError.ComponentDataElementPositionInComposite.Value.ToString());
                    }
                    if (!string.IsNullOrEmpty(elemError.DataElementReferenceNumber))
                    {
                        ak4.SetElement(3, elemError.DataElementReferenceNumber);
                    }
                    if (!string.IsNullOrEmpty(elemError.DataElementSyntaxErrorCode))
                    {
                        ak4.SetElement(4, elemError.DataElementSyntaxErrorCode);
                    }
                    if (!string.IsNullOrEmpty(elemError.CopyOfBadDataElement))
                    {
                        ak4.SetElement(5, elemError.CopyOfBadDataElement);
                    }
                    transactionSet.Segments.Add(ak4);
                }
            }

            // AK5 - Transaction Set Response Trailer
            var ak5 = new X12Segment { SegmentId = "AK5" };
            ak5.SetElement(1, tsResponse.TransactionSetAcknowledgmentCode);
            if (!string.IsNullOrEmpty(tsResponse.TransactionSetSyntaxErrorCode1))
            {
                ak5.SetElement(2, tsResponse.TransactionSetSyntaxErrorCode1);
            }
            transactionSet.Segments.Add(ak5);
        }

        // AK9 - Functional Group Response Trailer
        var ak9 = new X12Segment { SegmentId = "AK9" };
        ak9.SetElement(1, acknowledgment.FunctionalGroupAcknowledgmentCode);
        ak9.SetElement(2, acknowledgment.NumberOfTransactionSetsIncluded.ToString());
        ak9.SetElement(3, acknowledgment.NumberOfReceivedTransactionSets.ToString());
        ak9.SetElement(4, acknowledgment.NumberOfAcceptedTransactionSets.ToString());
        if (!string.IsNullOrEmpty(acknowledgment.FunctionalGroupSyntaxErrorCode1))
        {
            ak9.SetElement(5, acknowledgment.FunctionalGroupSyntaxErrorCode1);
        }
        transactionSet.Segments.Add(ak9);

        group.TransactionSets.Add(transactionSet);
        envelope.FunctionalGroups.Add(group);

        return _tokenizer.Build(envelope);
    }

    private bool ValidateTransactionSet(X12TransactionSet transactionSet, X12TransactionSet ackTransactionSet)
    {
        // Basic validation - check for required segments based on transaction type
        var hasErrors = false;

        switch (transactionSet.TransactionSetCode)
        {
            case "850":
                // Purchase Order should have BEG segment
                if (!transactionSet.Segments.Any(s => s.SegmentId == "BEG"))
                {
                    AddSegmentError(ackTransactionSet, "BEG", 0, "2"); // Missing required segment
                    hasErrors = true;
                }
                break;

            case "855":
                // PO Acknowledgment should have BAK segment
                if (!transactionSet.Segments.Any(s => s.SegmentId == "BAK"))
                {
                    AddSegmentError(ackTransactionSet, "BAK", 0, "2");
                    hasErrors = true;
                }
                break;

            case "856":
                // ASN should have BSN segment
                if (!transactionSet.Segments.Any(s => s.SegmentId == "BSN"))
                {
                    AddSegmentError(ackTransactionSet, "BSN", 0, "2");
                    hasErrors = true;
                }
                break;

            case "810":
                // Invoice should have BIG segment
                if (!transactionSet.Segments.Any(s => s.SegmentId == "BIG"))
                {
                    AddSegmentError(ackTransactionSet, "BIG", 0, "2");
                    hasErrors = true;
                }
                break;
        }

        return hasErrors;
    }

    private void AddSegmentError(X12TransactionSet ackTransactionSet, string segmentId, int position, string errorCode)
    {
        var ak3 = new X12Segment { SegmentId = "AK3" };
        ak3.SetElement(1, segmentId);
        ak3.SetElement(2, position.ToString());
        ak3.SetElement(4, errorCode);
        ackTransactionSet.Segments.Add(ak3);
    }
}

/// <summary>
/// Options for generating EDI 997 documents.
/// </summary>
public class Edi997GeneratorOptions
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
