using System.Text;
using Microsoft.Extensions.Logging;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent;

/// <summary>
/// Base parser for SPR content files (pipe-delimited text).
/// </summary>
public class SprContentFileParser
{
    private readonly ILogger<SprContentFileParser> _logger;
    private const char DefaultDelimiter = '|';

    public SprContentFileParser(ILogger<SprContentFileParser> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Parses a delimited file into records.
    /// </summary>
    public async IAsyncEnumerable<ParsedRecord> ParseFileAsync(
        StreamReader reader,
        bool hasHeader = true,
        char delimiter = DefaultDelimiter,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        string[]? headers = null;
        int lineNumber = 0;

        while (!reader.EndOfStream)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var line = await reader.ReadLineAsync(cancellationToken);
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = ParseLine(line, delimiter);

            if (hasHeader && headers == null)
            {
                headers = fields;
                continue;
            }

            yield return new ParsedRecord
            {
                LineNumber = lineNumber,
                Fields = fields,
                Headers = headers,
                RawLine = line
            };
        }
    }

    /// <summary>
    /// Parses a single line respecting quoted fields.
    /// </summary>
    private string[] ParseLine(string line, char delimiter)
    {
        var fields = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;
        bool prevWasQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && prevWasQuote)
                {
                    // Escaped quote
                    currentField.Append('"');
                    prevWasQuote = false;
                }
                else if (inQuotes)
                {
                    prevWasQuote = true;
                }
                else
                {
                    inQuotes = true;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                fields.Add(currentField.ToString().Trim());
                currentField.Clear();
                prevWasQuote = false;
            }
            else
            {
                if (prevWasQuote)
                {
                    // End of quoted field
                    inQuotes = false;
                    prevWasQuote = false;

                    // Check if this character is the delimiter
                    if (c == delimiter)
                    {
                        fields.Add(currentField.ToString().Trim());
                        currentField.Clear();
                        continue;
                    }
                }
                currentField.Append(c);
            }
        }

        fields.Add(currentField.ToString().Trim());
        return fields.ToArray();
    }

    /// <summary>
    /// Gets a field value by index with null safety.
    /// </summary>
    public static string GetField(string[] fields, int index, string defaultValue = "")
    {
        if (index < 0 || index >= fields.Length)
            return defaultValue;
        return string.IsNullOrWhiteSpace(fields[index]) ? defaultValue : fields[index].Trim();
    }

    /// <summary>
    /// Gets a field value by header name.
    /// </summary>
    public static string GetField(ParsedRecord record, string headerName, string defaultValue = "")
    {
        if (record.Headers == null)
            return defaultValue;

        var index = Array.FindIndex(record.Headers,
            h => h.Equals(headerName, StringComparison.OrdinalIgnoreCase));

        return GetField(record.Fields, index, defaultValue);
    }

    /// <summary>
    /// Parses an integer field.
    /// </summary>
    public static int? ParseInt(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return int.TryParse(value, out var result) ? result : null;
    }

    /// <summary>
    /// Parses a decimal field.
    /// </summary>
    public static decimal? ParseDecimal(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return decimal.TryParse(value, out var result) ? result : null;
    }

    /// <summary>
    /// Parses a date field.
    /// </summary>
    public static DateTime? ParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return DateTime.TryParse(value, out var result) ? result : null;
    }

    /// <summary>
    /// Sanitizes HTML content.
    /// </summary>
    public static string SanitizeHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Basic sanitization - remove dangerous script tags
        var sanitized = System.Text.RegularExpressions.Regex.Replace(
            html,
            @"<script[^>]*>[\s\S]*?</script>",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Remove on* event handlers
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"\son\w+\s*=\s*(['""])[^'""]*\1",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return sanitized;
    }
}

/// <summary>
/// A parsed record from a content file.
/// </summary>
public class ParsedRecord
{
    public int LineNumber { get; set; }
    public string[] Fields { get; set; } = Array.Empty<string>();
    public string[]? Headers { get; set; }
    public string RawLine { get; set; } = string.Empty;

    /// <summary>
    /// Gets a field by index.
    /// </summary>
    public string this[int index] => SprContentFileParser.GetField(Fields, index);

    /// <summary>
    /// Gets a field by header name.
    /// </summary>
    public string this[string header] => SprContentFileParser.GetField(this, header);
}
