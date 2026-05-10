using Vendorea.PartnerConnect.Edi.X12.Models;

namespace Vendorea.PartnerConnect.Edi.X12.Parser;

/// <summary>
/// Generic X12 parser that parses EDI documents into envelope/segment structures.
/// </summary>
public class X12Parser
{
    private readonly X12Tokenizer _tokenizer;

    public X12Parser()
    {
        _tokenizer = new X12Tokenizer();
    }

    /// <summary>
    /// Parses an X12 document string into an envelope structure.
    /// </summary>
    public X12ParseResult Parse(string ediContent)
    {
        var tokenResult = _tokenizer.Tokenize(ediContent);

        if (!tokenResult.Success)
        {
            return new X12ParseResult
            {
                Success = false,
                ErrorMessage = tokenResult.ErrorMessage
            };
        }

        var result = new X12ParseResult
        {
            Success = true,
            ElementSeparator = tokenResult.ElementSeparator,
            ComponentSeparator = tokenResult.ComponentSeparator,
            SegmentTerminator = tokenResult.SegmentTerminator
        };

        try
        {
            result.Envelope = ParseEnvelope(tokenResult.Segments);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Error parsing envelope: {ex.Message}";
        }

        return result;
    }

    private X12Envelope ParseEnvelope(List<X12Segment> segments)
    {
        var envelope = new X12Envelope();
        var segmentIndex = 0;

        // Parse ISA
        var isa = segments[segmentIndex++];
        if (isa.SegmentId != "ISA")
        {
            throw new InvalidOperationException("Expected ISA segment");
        }

        envelope.AuthorizationQualifier = isa.GetElement(1);
        envelope.AuthorizationInformation = isa.GetElement(2);
        envelope.SecurityQualifier = isa.GetElement(3);
        envelope.SecurityInformation = isa.GetElement(4);
        envelope.SenderQualifier = isa.GetElement(5);
        envelope.SenderId = isa.GetElement(6);
        envelope.ReceiverQualifier = isa.GetElement(7);
        envelope.ReceiverId = isa.GetElement(8);
        envelope.InterchangeDate = isa.GetElement(9);
        envelope.InterchangeTime = isa.GetElement(10);
        envelope.RepetitionSeparator = isa.GetElement(11).FirstOrDefault();
        envelope.InterchangeVersion = isa.GetElement(12);
        envelope.InterchangeControlNumber = isa.GetElement(13);
        envelope.AcknowledgmentRequested = isa.GetElement(14);
        envelope.UsageIndicator = isa.GetElement(15);
        envelope.ComponentSeparator = isa.GetElement(16).FirstOrDefault();

        // Parse functional groups
        while (segmentIndex < segments.Count)
        {
            var segment = segments[segmentIndex];

            if (segment.SegmentId == "GS")
            {
                var group = ParseFunctionalGroup(segments, ref segmentIndex);
                envelope.FunctionalGroups.Add(group);
            }
            else if (segment.SegmentId == "IEA")
            {
                // End of interchange
                break;
            }
            else
            {
                segmentIndex++;
            }
        }

        return envelope;
    }

    private X12FunctionalGroup ParseFunctionalGroup(List<X12Segment> segments, ref int segmentIndex)
    {
        var gs = segments[segmentIndex++];
        var group = new X12FunctionalGroup
        {
            FunctionalIdentifier = gs.GetElement(1),
            SenderCode = gs.GetElement(2),
            ReceiverCode = gs.GetElement(3),
            Date = gs.GetElement(4),
            Time = gs.GetElement(5),
            GroupControlNumber = gs.GetElement(6),
            ResponsibleAgencyCode = gs.GetElement(7),
            VersionCode = gs.GetElement(8)
        };

        // Parse transaction sets
        while (segmentIndex < segments.Count)
        {
            var segment = segments[segmentIndex];

            if (segment.SegmentId == "ST")
            {
                var transactionSet = ParseTransactionSet(segments, ref segmentIndex);
                group.TransactionSets.Add(transactionSet);
            }
            else if (segment.SegmentId == "GE")
            {
                // End of functional group
                segmentIndex++;
                break;
            }
            else
            {
                segmentIndex++;
            }
        }

        return group;
    }

    private X12TransactionSet ParseTransactionSet(List<X12Segment> segments, ref int segmentIndex)
    {
        var st = segments[segmentIndex++];
        var transactionSet = new X12TransactionSet
        {
            TransactionSetCode = st.GetElement(1),
            ControlNumber = st.GetElement(2)
        };

        // Parse segments until SE
        while (segmentIndex < segments.Count)
        {
            var segment = segments[segmentIndex];

            if (segment.SegmentId == "SE")
            {
                // End of transaction set
                segmentIndex++;
                break;
            }

            transactionSet.Segments.Add(segment);
            segmentIndex++;
        }

        return transactionSet;
    }
}

/// <summary>
/// Result of X12 parsing.
/// </summary>
public class X12ParseResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public char ElementSeparator { get; set; } = '*';
    public char ComponentSeparator { get; set; } = ':';
    public char SegmentTerminator { get; set; } = '~';
    public X12Envelope? Envelope { get; set; }
}
