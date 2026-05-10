namespace Vendorea.PartnerConnect.Edi.X12.Models;

/// <summary>
/// Represents the ISA/IEA interchange envelope.
/// </summary>
public class X12Envelope
{
    /// <summary>
    /// ISA01: Authorization Information Qualifier
    /// </summary>
    public string AuthorizationQualifier { get; set; } = "00";

    /// <summary>
    /// ISA02: Authorization Information
    /// </summary>
    public string AuthorizationInformation { get; set; } = string.Empty.PadRight(10);

    /// <summary>
    /// ISA03: Security Information Qualifier
    /// </summary>
    public string SecurityQualifier { get; set; } = "00";

    /// <summary>
    /// ISA04: Security Information
    /// </summary>
    public string SecurityInformation { get; set; } = string.Empty.PadRight(10);

    /// <summary>
    /// ISA05: Interchange ID Qualifier (Sender)
    /// </summary>
    public string SenderQualifier { get; set; } = "ZZ";

    /// <summary>
    /// ISA06: Interchange Sender ID
    /// </summary>
    public string SenderId { get; set; } = string.Empty;

    /// <summary>
    /// ISA07: Interchange ID Qualifier (Receiver)
    /// </summary>
    public string ReceiverQualifier { get; set; } = "ZZ";

    /// <summary>
    /// ISA08: Interchange Receiver ID
    /// </summary>
    public string ReceiverId { get; set; } = string.Empty;

    /// <summary>
    /// ISA09: Interchange Date (YYMMDD)
    /// </summary>
    public string InterchangeDate { get; set; } = DateTime.UtcNow.ToString("yyMMdd");

    /// <summary>
    /// ISA10: Interchange Time (HHMM)
    /// </summary>
    public string InterchangeTime { get; set; } = DateTime.UtcNow.ToString("HHmm");

    /// <summary>
    /// ISA11: Repetition Separator
    /// </summary>
    public char RepetitionSeparator { get; set; } = '^';

    /// <summary>
    /// ISA12: Interchange Control Version Number
    /// </summary>
    public string InterchangeVersion { get; set; } = "00501";

    /// <summary>
    /// ISA13: Interchange Control Number
    /// </summary>
    public string InterchangeControlNumber { get; set; } = "000000001";

    /// <summary>
    /// ISA14: Acknowledgment Requested
    /// </summary>
    public string AcknowledgmentRequested { get; set; } = "0";

    /// <summary>
    /// ISA15: Usage Indicator (P=Production, T=Test)
    /// </summary>
    public string UsageIndicator { get; set; } = "P";

    /// <summary>
    /// ISA16: Component Element Separator
    /// </summary>
    public char ComponentSeparator { get; set; } = ':';

    /// <summary>
    /// Functional groups within this interchange.
    /// </summary>
    public List<X12FunctionalGroup> FunctionalGroups { get; set; } = new();
}

/// <summary>
/// Represents the GS/GE functional group envelope.
/// </summary>
public class X12FunctionalGroup
{
    /// <summary>
    /// GS01: Functional Identifier Code
    /// </summary>
    public string FunctionalIdentifier { get; set; } = string.Empty;

    /// <summary>
    /// GS02: Application Sender's Code
    /// </summary>
    public string SenderCode { get; set; } = string.Empty;

    /// <summary>
    /// GS03: Application Receiver's Code
    /// </summary>
    public string ReceiverCode { get; set; } = string.Empty;

    /// <summary>
    /// GS04: Date (CCYYMMDD)
    /// </summary>
    public string Date { get; set; } = DateTime.UtcNow.ToString("yyyyMMdd");

    /// <summary>
    /// GS05: Time (HHMM or HHMMSS or HHMMSSD)
    /// </summary>
    public string Time { get; set; } = DateTime.UtcNow.ToString("HHmmss");

    /// <summary>
    /// GS06: Group Control Number
    /// </summary>
    public string GroupControlNumber { get; set; } = "1";

    /// <summary>
    /// GS07: Responsible Agency Code
    /// </summary>
    public string ResponsibleAgencyCode { get; set; } = "X";

    /// <summary>
    /// GS08: Version/Release/Industry Identifier Code
    /// </summary>
    public string VersionCode { get; set; } = "005010";

    /// <summary>
    /// Transaction sets within this functional group.
    /// </summary>
    public List<X12TransactionSet> TransactionSets { get; set; } = new();
}

/// <summary>
/// Represents a transaction set (ST/SE).
/// </summary>
public class X12TransactionSet
{
    /// <summary>
    /// ST01: Transaction Set Identifier Code (850, 855, etc.)
    /// </summary>
    public string TransactionSetCode { get; set; } = string.Empty;

    /// <summary>
    /// ST02: Transaction Set Control Number
    /// </summary>
    public string ControlNumber { get; set; } = "0001";

    /// <summary>
    /// Segments within this transaction set.
    /// </summary>
    public List<X12Segment> Segments { get; set; } = new();
}
