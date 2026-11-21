using System.Text;
using FluentAssertions;
using Vendorea.PartnerConnect.Infrastructure.SprContent;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

/// <summary>
/// Decoding documents received from SPR.
/// </summary>
/// <remarks>
/// Inbound documents were read with <c>Encoding.UTF8</c>, whose replacement fallback does not
/// throw: it substitutes U+FFFD. SPR's files are Windows-1252, so every non-ASCII character in an
/// acknowledgement, ASN or invoice was silently destroyed on the way in.
///
/// We found it in the acknowledgement for WPO-20261121-0001, where the em dash SPR echoed back
/// arrived as codepoint 65533. That single byte is also why their EZPO4 intake rejects
/// multi-byte UTF-8 - the stack is single-byte in both directions.
/// </remarks>
public class SprTextDecoderTests
{
    private const char Replacement = '�';
    private const string EmDash = "—";

    [Fact]
    public void A_windows_1252_em_dash_survives_instead_of_becoming_a_replacement_character()
    {
        // 0x97 is the em dash in Windows-1252 and an invalid lead byte in UTF-8. This is the
        // exact byte that arrived in SPR's acknowledgement.
        var bytes = Encoding.ASCII.GetBytes("Customer1 X Attn: John Smith");
        bytes[10] = 0x97;

        var decoded = SprTextDecoder.Decode(bytes);

        decoded.Should().Be("Customer1 " + EmDash + " Attn: John Smith");
        decoded.Should().NotContain(Replacement.ToString());
    }

    [Fact]
    public void The_old_behaviour_is_what_this_replaces()
    {
        // Pinning the defect so the regression is visible: the previous reader produced U+FFFD
        // for the very bytes the new one decodes correctly.
        var bytes = Encoding.ASCII.GetBytes("Customer1 X Attn");
        bytes[10] = 0x97;

        Encoding.UTF8.GetString(bytes).Should().Contain(Replacement.ToString());
        SprTextDecoder.Decode(bytes).Should().NotContain(Replacement.ToString());
    }

    [Theory]
    [InlineData(0x91, '‘')]  // left single quote
    [InlineData(0x92, '’')]  // right single quote
    [InlineData(0x93, '“')]  // left double quote
    [InlineData(0x94, '”')]  // right double quote
    [InlineData(0x96, '–')]  // en dash
    [InlineData(0x97, '—')]  // em dash
    [InlineData(0x85, '…')]  // ellipsis
    [InlineData(0x80, '€')]  // euro
    public void The_windows_1252_high_range_maps_to_printable_characters(byte raw, char expected)
    {
        // ISO-8859-1 would render all of these as C1 control characters instead.
        SprTextDecoder.Decode(new[] { raw }).Should().Be(expected.ToString());
    }

    [Fact]
    public void Latin1_accented_letters_decode_correctly()
    {
        // 0xF1 is n-tilde in both Latin-1 and Windows-1252.
        var bytes = new byte[] { (byte)'P', (byte)'e', 0xF1, (byte)'a' };

        SprTextDecoder.Decode(bytes).Should().Be("Peña");
    }

    [Fact]
    public void Valid_utf8_is_decoded_as_utf8()
    {
        // If SPR ever modernises, or another partner reuses this path, a real UTF-8 document must
        // not be mangled by a hardcoded assumption.
        var bytes = Encoding.UTF8.GetBytes("Peña " + EmDash + " café");

        SprTextDecoder.Decode(bytes).Should().Be("Peña " + EmDash + " café");
    }

    [Fact]
    public void A_utf8_bom_is_honoured_and_stripped()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }
            .Concat(Encoding.UTF8.GetBytes("<Order/>")).ToArray();

        SprTextDecoder.Decode(bytes).Should().Be("<Order/>");
    }

    [Fact]
    public void Ascii_decodes_identically_either_way()
    {
        // The overwhelming majority of SPR traffic. Nothing about it changes.
        const string ascii = "<Order BuyerOrganizationCode=\"0033822.00\" OrderType=\"03\" />";

        SprTextDecoder.Decode(Encoding.ASCII.GetBytes(ascii)).Should().Be(ascii);
    }

    [Fact]
    public void An_empty_document_decodes_to_empty()
    {
        SprTextDecoder.Decode(Array.Empty<byte>()).Should().BeEmpty();
    }

    [Fact]
    public void Utf8_validity_is_reported_so_the_charset_can_be_logged()
    {
        SprTextDecoder.IsValidUtf8(Encoding.UTF8.GetBytes("café")).Should().BeTrue();
        SprTextDecoder.IsValidUtf8(new byte[] { 0x97 }).Should().BeFalse();
        SprTextDecoder.IsValidUtf8(Encoding.ASCII.GetBytes("plain")).Should().BeTrue();
    }

    [Fact]
    public async Task A_stream_decodes_the_same_as_its_bytes()
    {
        var bytes = Encoding.ASCII.GetBytes("Customer1 X Attn");
        bytes[10] = 0x97;
        using var stream = new MemoryStream(bytes);

        var decoded = await SprTextDecoder.DecodeAsync(stream);

        decoded.Should().Be(SprTextDecoder.Decode(bytes));
    }

    [Fact]
    public void A_real_acknowledgement_decodes_without_loss()
    {
        // The shape of SPR's POACK, with the em dash they echoed back as a single byte.
        // '~' marks where the single 0x97 byte goes; it appears nowhere else in the document.
        var xml = "<Order><Errors><Error Message=\"XML Schema Validation Failed\">"
                + "<OriginalOrder>FirstName=\"Customer1 ~ Attn: John Smith\"</OriginalOrder>"
                + "</Error></Errors></Order>";
        var bytes = Encoding.ASCII.GetBytes(xml);
        bytes[Array.IndexOf(bytes, (byte)'~')] = 0x97;

        var decoded = SprTextDecoder.Decode(bytes);

        decoded.Should().Contain("Customer1 " + EmDash + " Attn: John Smith");
        decoded.Should().NotContain(Replacement.ToString());
        var act = () => System.Xml.Linq.XDocument.Parse(decoded);
        act.Should().NotThrow();
    }
}
