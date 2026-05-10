namespace Vendorea.PartnerConnect.Edi.X12.Models;

/// <summary>
/// Represents an X12 segment with its elements.
/// </summary>
public class X12Segment
{
    /// <summary>
    /// The segment identifier (e.g., ISA, GS, ST, BEG, PO1).
    /// </summary>
    public string SegmentId { get; set; } = string.Empty;

    /// <summary>
    /// The elements within the segment.
    /// </summary>
    public List<string> Elements { get; set; } = new();

    /// <summary>
    /// Gets an element at the specified position (1-based index).
    /// </summary>
    public string GetElement(int position)
    {
        if (position < 1 || position > Elements.Count)
        {
            return string.Empty;
        }
        return Elements[position - 1];
    }

    /// <summary>
    /// Gets an element at the specified position (1-based index), with a default value.
    /// </summary>
    public string GetElement(int position, string defaultValue)
    {
        var value = GetElement(position);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    /// <summary>
    /// Gets an element as an integer.
    /// </summary>
    public int GetElementAsInt(int position, int defaultValue = 0)
    {
        var value = GetElement(position);
        return int.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Gets an element as a decimal.
    /// </summary>
    public decimal GetElementAsDecimal(int position, decimal defaultValue = 0m)
    {
        var value = GetElement(position);
        return decimal.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    /// Gets an element as a DateTime.
    /// </summary>
    public DateTime? GetElementAsDate(int position, string format = "yyyyMMdd")
    {
        var value = GetElement(position);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return DateTime.TryParseExact(value, format, null,
            System.Globalization.DateTimeStyles.None, out var result)
            ? result
            : null;
    }

    /// <summary>
    /// Sets an element at the specified position (1-based index).
    /// </summary>
    public void SetElement(int position, string value)
    {
        // Ensure the list is large enough
        while (Elements.Count < position)
        {
            Elements.Add(string.Empty);
        }
        Elements[position - 1] = value ?? string.Empty;
    }

    /// <summary>
    /// Creates a segment from a string.
    /// </summary>
    public static X12Segment Parse(string segmentString, char elementSeparator)
    {
        var parts = segmentString.Split(elementSeparator);
        return new X12Segment
        {
            SegmentId = parts.Length > 0 ? parts[0] : string.Empty,
            Elements = parts.Length > 1 ? parts.Skip(1).ToList() : new List<string>()
        };
    }

    /// <summary>
    /// Converts the segment to a string.
    /// </summary>
    public string ToString(char elementSeparator)
    {
        return SegmentId + elementSeparator + string.Join(elementSeparator, Elements);
    }

    public override string ToString()
    {
        return ToString('*');
    }
}
