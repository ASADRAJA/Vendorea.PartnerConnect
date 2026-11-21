using System.Text;

namespace Vendorea.PartnerConnect.Infrastructure.SprContent;

/// <summary>
/// Decodes a document received from SPR into text.
/// </summary>
/// <remarks>
/// SPR's files are not UTF-8. We read them with <c>Encoding.UTF8</c>, which uses a replacement
/// fallback rather than throwing, so every byte it could not decode became U+FFFD and nothing
/// logged it. Item descriptions, supplier names and addresses were losing characters silently.
///
/// We found this in the acknowledgement for a rejected order: SPR echoed our three-byte UTF-8 em
/// dash back as a single byte, and our reader turned it into U+FFFD. A single byte for that
/// character is what a Windows-1252 system produces (0x97), which also explains why their EZPO4
/// intake rejects multi-byte UTF-8.
///
/// So: try UTF-8 strictly, and fall back to Windows-1252. Trying UTF-8 first means a document
/// that genuinely is UTF-8 - if SPR ever modernises, or another partner reuses this path -
/// decodes correctly rather than being mangled by a hardcoded assumption. Windows-1252 as the
/// fallback never throws, so decoding always yields something.
/// </remarks>
public static class SprTextDecoder
{
    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>
    /// The 27 positions where Windows-1252 differs from ISO-8859-1.
    /// </summary>
    /// <remarks>
    /// ISO-8859-1 maps 0x80-0x9F to C1 control characters; Windows-1252 puts printable
    /// punctuation there instead - including the em dash at 0x97 that led us here. Decoding with
    /// <c>Encoding.Latin1</c> and patching this range avoids a dependency on
    /// System.Text.Encoding.CodePages and a provider registration in every host. The five
    /// positions Windows-1252 leaves undefined (0x81, 0x8D, 0x8F, 0x90, 0x9D) are absent here and
    /// keep their Latin-1 value.
    /// </remarks>
    private static readonly Dictionary<char, char> Cp1252HighRange = new()
    {
        ['\u0080'] = '\u20AC', // euro sign,
        ['\u0082'] = '\u201A', // single low-9 quote,
        ['\u0083'] = '\u0192', // florin,
        ['\u0084'] = '\u201E', // double low-9 quote,
        ['\u0085'] = '\u2026', // ellipsis,
        ['\u0086'] = '\u2020', // dagger,
        ['\u0087'] = '\u2021', // double dagger,
        ['\u0088'] = '\u02C6', // circumflex,
        ['\u0089'] = '\u2030', // per mille,
        ['\u008A'] = '\u0160', // S caron,
        ['\u008B'] = '\u2039', // single left angle quote,
        ['\u008C'] = '\u0152', // OE ligature,
        ['\u008E'] = '\u017D', // Z caron,
        ['\u0091'] = '\u2018', // left single quote,
        ['\u0092'] = '\u2019', // right single quote,
        ['\u0093'] = '\u201C', // left double quote,
        ['\u0094'] = '\u201D', // right double quote,
        ['\u0095'] = '\u2022', // bullet,
        ['\u0096'] = '\u2013', // en dash,
        ['\u0097'] = '\u2014', // EM DASH - the character that exposed all of this,
        ['\u0098'] = '\u02DC', // small tilde,
        ['\u0099'] = '\u2122', // trade mark,
        ['\u009A'] = '\u0161', // s caron,
        ['\u009B'] = '\u203A', // single right angle quote,
        ['\u009C'] = '\u0153', // oe ligature,
        ['\u009E'] = '\u017E', // z caron,
        ['\u009F'] = '\u0178', // Y diaeresis
    };

    /// <summary>
    /// Decodes <paramref name="bytes"/>, preferring UTF-8 and falling back to Windows-1252.
    /// </summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length == 0)
            return string.Empty;

        // A UTF-8 BOM settles it outright.
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return StrictUtf8.GetString(bytes, 3, bytes.Length - 3);

        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return DecodeCp1252(bytes);
        }
    }

    /// <summary>Reads the stream to the end and decodes it.</summary>
    public static async Task<string> DecodeAsync(
        Stream stream, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        return Decode(buffer.ToArray());
    }

    /// <summary>
    /// True when the bytes are valid UTF-8. Callers log the answer rather than let the character
    /// set of an inbound document go unrecorded.
    /// </summary>
    public static bool IsValidUtf8(byte[] bytes)
    {
        try
        {
            StrictUtf8.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static string DecodeCp1252(byte[] bytes)
    {
        var latin1 = Encoding.Latin1.GetString(bytes);

        var builder = new StringBuilder(latin1.Length);
        foreach (var ch in latin1)
            builder.Append(Cp1252HighRange.TryGetValue(ch, out var mapped) ? mapped : ch);

        return builder.ToString();
    }
}
