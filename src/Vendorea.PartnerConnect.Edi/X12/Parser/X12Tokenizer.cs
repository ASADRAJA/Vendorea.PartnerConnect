using Vendorea.PartnerConnect.Edi.X12.Models;

namespace Vendorea.PartnerConnect.Edi.X12.Parser;

/// <summary>
/// Tokenizes X12 EDI documents into segments.
/// </summary>
public class X12Tokenizer
{
    /// <summary>
    /// Default element separator.
    /// </summary>
    public const char DefaultElementSeparator = '*';

    /// <summary>
    /// Default segment terminator.
    /// </summary>
    public const char DefaultSegmentTerminator = '~';

    /// <summary>
    /// Default component separator.
    /// </summary>
    public const char DefaultComponentSeparator = ':';

    /// <summary>
    /// Tokenizes an X12 document string into segments.
    /// </summary>
    public X12TokenizerResult Tokenize(string ediContent)
    {
        if (string.IsNullOrWhiteSpace(ediContent))
        {
            return new X12TokenizerResult
            {
                Success = false,
                ErrorMessage = "EDI content is empty"
            };
        }

        // Clean up the content
        ediContent = ediContent.Trim();

        // Detect delimiters from ISA segment
        if (!ediContent.StartsWith("ISA", StringComparison.OrdinalIgnoreCase))
        {
            return new X12TokenizerResult
            {
                Success = false,
                ErrorMessage = "Invalid X12 document: must start with ISA segment"
            };
        }

        // ISA segment is fixed length - element separator is at position 3
        if (ediContent.Length < 106)
        {
            return new X12TokenizerResult
            {
                Success = false,
                ErrorMessage = "Invalid ISA segment: too short"
            };
        }

        var elementSeparator = ediContent[3];

        // Find segment terminator (character after ISA16)
        // ISA has 16 elements, and position 105 (0-indexed) contains ISA16
        // The segment terminator follows ISA16
        var componentSeparator = ediContent[104]; // ISA16
        var segmentTerminator = ediContent[105];  // Character after ISA16

        // Handle case where there might be newlines as well
        if (segmentTerminator == '\r' || segmentTerminator == '\n')
        {
            segmentTerminator = DefaultSegmentTerminator;
        }

        var result = new X12TokenizerResult
        {
            Success = true,
            ElementSeparator = elementSeparator,
            ComponentSeparator = componentSeparator,
            SegmentTerminator = segmentTerminator,
            Segments = new List<X12Segment>()
        };

        // Split into segments
        var segmentStrings = ediContent
            .Replace("\r", "")
            .Replace("\n", "")
            .Split(new[] { segmentTerminator }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var segmentString in segmentStrings)
        {
            var trimmed = segmentString.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            var segment = X12Segment.Parse(trimmed, elementSeparator);
            result.Segments.Add(segment);
        }

        return result;
    }

    /// <summary>
    /// Builds a complete X12 document string from an envelope.
    /// </summary>
    public string Build(X12Envelope envelope, char elementSeparator = '*', char segmentTerminator = '~')
    {
        var segments = new List<string>();

        // Build ISA segment
        var isa = $"ISA{elementSeparator}" +
            $"{envelope.AuthorizationQualifier.PadRight(2)}{elementSeparator}" +
            $"{envelope.AuthorizationInformation.PadRight(10)}{elementSeparator}" +
            $"{envelope.SecurityQualifier.PadRight(2)}{elementSeparator}" +
            $"{envelope.SecurityInformation.PadRight(10)}{elementSeparator}" +
            $"{envelope.SenderQualifier.PadRight(2)}{elementSeparator}" +
            $"{envelope.SenderId.PadRight(15)}{elementSeparator}" +
            $"{envelope.ReceiverQualifier.PadRight(2)}{elementSeparator}" +
            $"{envelope.ReceiverId.PadRight(15)}{elementSeparator}" +
            $"{envelope.InterchangeDate}{elementSeparator}" +
            $"{envelope.InterchangeTime}{elementSeparator}" +
            $"{envelope.RepetitionSeparator}{elementSeparator}" +
            $"{envelope.InterchangeVersion}{elementSeparator}" +
            $"{envelope.InterchangeControlNumber.PadLeft(9, '0')}{elementSeparator}" +
            $"{envelope.AcknowledgmentRequested}{elementSeparator}" +
            $"{envelope.UsageIndicator}{elementSeparator}" +
            $"{envelope.ComponentSeparator}";
        segments.Add(isa);

        var totalTransactionSets = 0;

        foreach (var group in envelope.FunctionalGroups)
        {
            // Build GS segment
            var gs = $"GS{elementSeparator}" +
                $"{group.FunctionalIdentifier}{elementSeparator}" +
                $"{group.SenderCode}{elementSeparator}" +
                $"{group.ReceiverCode}{elementSeparator}" +
                $"{group.Date}{elementSeparator}" +
                $"{group.Time}{elementSeparator}" +
                $"{group.GroupControlNumber}{elementSeparator}" +
                $"{group.ResponsibleAgencyCode}{elementSeparator}" +
                $"{group.VersionCode}";
            segments.Add(gs);

            foreach (var transactionSet in group.TransactionSets)
            {
                totalTransactionSets++;

                // Build ST segment
                var st = $"ST{elementSeparator}{transactionSet.TransactionSetCode}{elementSeparator}{transactionSet.ControlNumber}";
                segments.Add(st);

                // Add all segments
                foreach (var segment in transactionSet.Segments)
                {
                    segments.Add(segment.ToString(elementSeparator));
                }

                // Build SE segment
                var segmentCount = transactionSet.Segments.Count + 2; // Include ST and SE
                var se = $"SE{elementSeparator}{segmentCount}{elementSeparator}{transactionSet.ControlNumber}";
                segments.Add(se);
            }

            // Build GE segment
            var ge = $"GE{elementSeparator}{group.TransactionSets.Count}{elementSeparator}{group.GroupControlNumber}";
            segments.Add(ge);
        }

        // Build IEA segment
        var iea = $"IEA{elementSeparator}{envelope.FunctionalGroups.Count}{elementSeparator}{envelope.InterchangeControlNumber.PadLeft(9, '0')}";
        segments.Add(iea);

        return string.Join(segmentTerminator.ToString(), segments) + segmentTerminator;
    }
}

/// <summary>
/// Result of X12 tokenization.
/// </summary>
public class X12TokenizerResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public char ElementSeparator { get; set; } = '*';
    public char ComponentSeparator { get; set; } = ':';
    public char SegmentTerminator { get; set; } = '~';
    public List<X12Segment> Segments { get; set; } = new();
}
