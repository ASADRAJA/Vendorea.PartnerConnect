namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Parsers;

/// <summary>
/// Parses SPR's "SPR Inventory Status" detailed on-hand file (sprfull.ezoh), a fixed-width flat
/// file per SPRCP-01020 / SPRFL-02000:
///   H0 — header (first record; identifiers — not needed for inventory, skipped)
///   I1 — lists the SPR DC numbers (3-digit) in column order, starting at position 18
///   Q1 — one per SKU: RECTYP(2) ITEMNO(15) ASTAT(1) SPRUOM(2) then a 6-digit quantity-on-hand
///        per DC, positionally aligned to the I1 DC list (ETA columns, if present, follow and are
///        not parsed here).
/// Only positive per-DC quantities are retained (0 = not stocked at that DC).
/// </summary>
public static class SprEzohInventoryParser
{
    private const int ItemNoStart = 2;       // 0-indexed; RECTYP occupies 0..1
    private const int ItemNoLength = 15;
    private const int StatusPos = 17;
    private const int UomStart = 18;
    private const int UomLength = 2;
    private const int QtyStart = 20;
    private const int QtyWidth = 6;
    private const int DcCodeWidth = 3;

    public static SprEzohParseResult Parse(string content)
    {
        using var reader = new StringReader(content ?? string.Empty);
        return Parse(reader);
    }

    public static SprEzohParseResult Parse(TextReader reader)
    {
        var result = new SprEzohParseResult();
        var dcNumbers = new List<int>();

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            result.LineCount++;
            if (line.Length < 2) continue;

            var recordType = line.Substring(0, 2);
            switch (recordType)
            {
                case "I1":
                    dcNumbers = ParseDcList(line);
                    result.DcNumbers = dcNumbers;
                    break;

                case "Q1":
                    var item = ParseItem(line, dcNumbers);
                    if (item != null) result.Items.Add(item);
                    break;

                // "H0" header and anything else are ignored.
            }
        }

        return result;
    }

    private static List<int> ParseDcList(string line)
    {
        var dcNumbers = new List<int>();
        if (line.Length <= StatusPos) return dcNumbers;

        // DC codes begin at position 18 (1-indexed) = index 17; ITEMNO (cols 3-17) is "Not Used".
        var payload = line.Substring(StatusPos);
        for (var i = 0; i + DcCodeWidth <= payload.Length; i += DcCodeWidth)
        {
            var token = payload.Substring(i, DcCodeWidth).Trim();
            if (int.TryParse(token, out var dc)) dcNumbers.Add(dc);
        }
        return dcNumbers;
    }

    private static SprEzohItem? ParseItem(string line, IReadOnlyList<int> dcNumbers)
    {
        if (line.Length < UomStart) return null;

        var itemNumber = Slice(line, ItemNoStart, ItemNoLength).Trim();
        if (string.IsNullOrEmpty(itemNumber)) return null;

        var status = line.Length > StatusPos ? line[StatusPos] : ' ';
        var uom = Slice(line, UomStart, UomLength).Trim();

        var item = new SprEzohItem
        {
            ItemNumber = itemNumber,
            StrippedItemNumber = StripSpecialChars(itemNumber),
            Status = status,
            UnitOfMeasure = string.IsNullOrEmpty(uom) ? "EA" : uom
        };

        for (var i = 0; i < dcNumbers.Count; i++)
        {
            var pos = QtyStart + (i * QtyWidth);
            if (pos + QtyWidth > line.Length) break; // record truncated (trailing zero DCs omitted)

            var token = line.Substring(pos, QtyWidth).Trim();
            if (int.TryParse(token, out var qty) && qty > 0)
                item.Quantities.Add(new SprDcQuantity(dcNumbers[i], qty));
        }

        return item;
    }

    private static string Slice(string line, int start, int length)
    {
        if (start >= line.Length) return string.Empty;
        return line.Substring(start, Math.Min(length, line.Length - start));
    }

    /// <summary>
    /// Strips the special characters SPR calls out (dashes, periods, slashes, spaces) so item
    /// numbers can be compared against other SPR lists (e.g. price records' StockNumberStripped).
    /// </summary>
    public static string StripSpecialChars(string itemNumber) =>
        new string(itemNumber.Where(c => c is not ('-' or '.' or '/' or ' ')).ToArray());
}

public class SprEzohParseResult
{
    public List<SprEzohItem> Items { get; } = new();
    public IReadOnlyList<int> DcNumbers { get; set; } = Array.Empty<int>();
    public int LineCount { get; set; }
}

public class SprEzohItem
{
    public string ItemNumber { get; set; } = string.Empty;
    public string StrippedItemNumber { get; set; } = string.Empty;
    /// <summary>'A' Active, 'D' Discontinued by SPR, 'X' Discontinued by MFR, 'E' Purge pending.</summary>
    public char Status { get; set; }
    public string UnitOfMeasure { get; set; } = "EA";
    /// <summary>Positive per-DC quantities only.</summary>
    public List<SprDcQuantity> Quantities { get; } = new();
    public int TotalQuantity => Quantities.Sum(q => q.Quantity);
}

public readonly record struct SprDcQuantity(int DcNumber, int Quantity);
