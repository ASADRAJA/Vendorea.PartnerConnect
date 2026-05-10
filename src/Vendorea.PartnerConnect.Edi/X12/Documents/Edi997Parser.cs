using Vendorea.PartnerConnect.Edi.X12.Models;
using Vendorea.PartnerConnect.Edi.X12.Parser;

namespace Vendorea.PartnerConnect.Edi.X12.Documents;

/// <summary>
/// Parser for EDI 997 Functional Acknowledgment documents.
/// </summary>
public class Edi997Parser
{
    private readonly X12Parser _parser;

    public Edi997Parser()
    {
        _parser = new X12Parser();
    }

    /// <summary>
    /// Parses an EDI 997 document into a FunctionalAcknowledgment model.
    /// </summary>
    public Edi997ParseResult Parse(string ediContent)
    {
        var parseResult = _parser.Parse(ediContent);

        if (!parseResult.Success || parseResult.Envelope == null)
        {
            return new Edi997ParseResult
            {
                Success = false,
                ErrorMessage = parseResult.ErrorMessage ?? "Failed to parse EDI document"
            };
        }

        var acknowledgments = new List<FunctionalAcknowledgment>();
        var errors = new List<string>();

        foreach (var group in parseResult.Envelope.FunctionalGroups)
        {
            foreach (var transactionSet in group.TransactionSets)
            {
                if (transactionSet.TransactionSetCode != "997")
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

        return new Edi997ParseResult
        {
            Success = errors.Count == 0,
            Acknowledgments = acknowledgments,
            Errors = errors,
            ErrorMessage = errors.Count > 0 ? string.Join("; ", errors) : null
        };
    }

    private FunctionalAcknowledgment ParseAcknowledgment(
        X12TransactionSet transactionSet,
        X12Envelope envelope)
    {
        var ack = new FunctionalAcknowledgment
        {
            SenderId = envelope.SenderId.Trim(),
            ReceiverId = envelope.ReceiverId.Trim(),
            TransactionSetResponses = new List<TransactionSetResponse>()
        };

        TransactionSetResponse? currentTsResponse = null;

        foreach (var segment in transactionSet.Segments)
        {
            switch (segment.SegmentId)
            {
                case "AK1":
                    // Functional group response header
                    ack.AcknowledgedFunctionalIdentifier = segment.GetElement(1);
                    ack.AcknowledgedGroupControlNumber = segment.GetElement(2);
                    break;

                case "AK2":
                    // Transaction set response header
                    currentTsResponse = new TransactionSetResponse
                    {
                        TransactionSetIdentifierCode = segment.GetElement(1),
                        TransactionSetControlNumber = segment.GetElement(2)
                    };
                    ack.TransactionSetResponses.Add(currentTsResponse);
                    break;

                case "AK3":
                    // Data segment note
                    if (currentTsResponse != null)
                    {
                        currentTsResponse.SegmentErrors.Add(new SegmentError
                        {
                            SegmentIdCode = segment.GetElement(1),
                            SegmentPositionInTransactionSet = segment.GetElementAsInt(2),
                            LoopIdentifierCode = segment.GetElement(3),
                            SegmentSyntaxErrorCode = segment.GetElement(4)
                        });
                    }
                    break;

                case "AK4":
                    // Data element note
                    if (currentTsResponse != null && currentTsResponse.SegmentErrors.Count > 0)
                    {
                        var lastSegmentError = currentTsResponse.SegmentErrors.Last();
                        lastSegmentError.ElementErrors.Add(new ElementError
                        {
                            ElementPositionInSegment = segment.GetElementAsInt(1),
                            ComponentDataElementPositionInComposite = segment.GetElementAsInt(2),
                            DataElementReferenceNumber = segment.GetElement(3),
                            DataElementSyntaxErrorCode = segment.GetElement(4),
                            CopyOfBadDataElement = segment.GetElement(5)
                        });
                    }
                    break;

                case "AK5":
                    // Transaction set response trailer
                    if (currentTsResponse != null)
                    {
                        currentTsResponse.TransactionSetAcknowledgmentCode = segment.GetElement(1);
                        currentTsResponse.TransactionSetSyntaxErrorCode1 = segment.GetElement(2);
                        currentTsResponse.TransactionSetSyntaxErrorCode2 = segment.GetElement(3);
                        currentTsResponse.TransactionSetSyntaxErrorCode3 = segment.GetElement(4);
                        currentTsResponse.TransactionSetSyntaxErrorCode4 = segment.GetElement(5);
                        currentTsResponse.TransactionSetSyntaxErrorCode5 = segment.GetElement(6);
                    }
                    break;

                case "AK9":
                    // Functional group response trailer
                    ack.FunctionalGroupAcknowledgmentCode = segment.GetElement(1);
                    ack.NumberOfTransactionSetsIncluded = segment.GetElementAsInt(2);
                    ack.NumberOfReceivedTransactionSets = segment.GetElementAsInt(3);
                    ack.NumberOfAcceptedTransactionSets = segment.GetElementAsInt(4);
                    ack.FunctionalGroupSyntaxErrorCode1 = segment.GetElement(5);
                    ack.FunctionalGroupSyntaxErrorCode2 = segment.GetElement(6);
                    break;
            }
        }

        return ack;
    }
}

/// <summary>
/// Result of EDI 997 parsing.
/// </summary>
public class Edi997ParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<FunctionalAcknowledgment> Acknowledgments { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Functional Acknowledgment (997) model.
/// </summary>
public class FunctionalAcknowledgment
{
    public string SenderId { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public string AcknowledgedFunctionalIdentifier { get; set; } = string.Empty;
    public string AcknowledgedGroupControlNumber { get; set; } = string.Empty;
    public string FunctionalGroupAcknowledgmentCode { get; set; } = string.Empty;
    public int NumberOfTransactionSetsIncluded { get; set; }
    public int NumberOfReceivedTransactionSets { get; set; }
    public int NumberOfAcceptedTransactionSets { get; set; }
    public string? FunctionalGroupSyntaxErrorCode1 { get; set; }
    public string? FunctionalGroupSyntaxErrorCode2 { get; set; }
    public List<TransactionSetResponse> TransactionSetResponses { get; set; } = new();

    /// <summary>
    /// Whether the functional group was accepted (A) or accepted with errors (E).
    /// </summary>
    public bool IsAccepted => FunctionalGroupAcknowledgmentCode == "A" || FunctionalGroupAcknowledgmentCode == "E";

    /// <summary>
    /// Whether the functional group was rejected (R).
    /// </summary>
    public bool IsRejected => FunctionalGroupAcknowledgmentCode == "R";
}

/// <summary>
/// Transaction set response within a functional acknowledgment.
/// </summary>
public class TransactionSetResponse
{
    public string TransactionSetIdentifierCode { get; set; } = string.Empty;
    public string TransactionSetControlNumber { get; set; } = string.Empty;
    public string TransactionSetAcknowledgmentCode { get; set; } = string.Empty;
    public string? TransactionSetSyntaxErrorCode1 { get; set; }
    public string? TransactionSetSyntaxErrorCode2 { get; set; }
    public string? TransactionSetSyntaxErrorCode3 { get; set; }
    public string? TransactionSetSyntaxErrorCode4 { get; set; }
    public string? TransactionSetSyntaxErrorCode5 { get; set; }
    public List<SegmentError> SegmentErrors { get; set; } = new();

    /// <summary>
    /// Whether the transaction set was accepted (A) or accepted with errors (E).
    /// </summary>
    public bool IsAccepted => TransactionSetAcknowledgmentCode == "A" || TransactionSetAcknowledgmentCode == "E";
}

/// <summary>
/// Segment error within a transaction set response.
/// </summary>
public class SegmentError
{
    public string SegmentIdCode { get; set; } = string.Empty;
    public int SegmentPositionInTransactionSet { get; set; }
    public string? LoopIdentifierCode { get; set; }
    public string? SegmentSyntaxErrorCode { get; set; }
    public List<ElementError> ElementErrors { get; set; } = new();
}

/// <summary>
/// Element error within a segment error.
/// </summary>
public class ElementError
{
    public int ElementPositionInSegment { get; set; }
    public int? ComponentDataElementPositionInComposite { get; set; }
    public string? DataElementReferenceNumber { get; set; }
    public string? DataElementSyntaxErrorCode { get; set; }
    public string? CopyOfBadDataElement { get; set; }
}
