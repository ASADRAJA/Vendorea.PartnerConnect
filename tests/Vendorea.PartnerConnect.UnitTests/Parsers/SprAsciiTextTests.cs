using FluentAssertions;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

/// <summary>
/// Folding outbound SPR text to the ASCII their intake actually accepts.
/// </summary>
/// <remarks>
/// SPR rejected two orders on 2026-11-21 over a single em dash in a ship-to name, reporting it as
/// "XML Schema Validation Failed" while their own nested report said
/// "Invalid byte 1 of 1-byte UTF-8 sequence" at line 3, column 42. A structurally identical order
/// on 2026-11-02 was accepted because it happened to be pure ASCII.
///
/// Invisible characters are written as escapes here, so each case says what it tests rather than
/// depending on what an editor chooses to render.
/// </remarks>
public class SprAsciiTextTests
{
    /// <summary>U+2014, the character that rejected both orders.</summary>
    private const string EmDash = "\u2014";

    [Fact]
    public void The_exact_value_that_SPR_rejected_becomes_sendable()
    {
        // Verbatim from Orders.ShipToJson on WPO-20261121-0001.
        var submitted = "Customer1 " + EmDash + " Attn: John Smith";

        SprAsciiText.Fold(submitted).Should().Be("Customer1 - Attn: John Smith");
    }

    [Fact]
    public void The_value_SPR_accepted_is_returned_untouched()
    {
        // From WPO-20261022-0001, which SPR accepted. Folding must be a no-op on ASCII - the
        // control that says this fix changes nothing which already worked.
        const string accepted = "John Smith";

        SprAsciiText.Fold(accepted).Should().BeSameAs(accepted);
    }

    [Theory]
    [InlineData("2821 coral valley")]
    [InlineData("Leander")]
    [InlineData("WPO-20261022-0001")]
    [InlineData("admin@dealer1.com")]
    [InlineData("0033822.00")]
    [InlineData("STCUSB3AUB50CMS")]
    public void Real_order_values_pass_through_unchanged(string value)
    {
        SprAsciiText.Fold(value).Should().Be(value);
    }

    // ---- the characters that actually turn up ----

    [Theory]
    [InlineData("—", "-")]            // em dash
    [InlineData("–", "-")]            // en dash
    [InlineData("‘x’", "'x'")]   // curly single quotes
    [InlineData("“x”", "\"x\"")] // curly double quotes
    [InlineData("…", "...")]          // ellipsis
    [InlineData("a\u00A0b", "a b")]            // non-breaking space
    [InlineData("•", "-")]            // bullet
    [InlineData("×", "x")]            // multiplication sign
    public void Typographic_characters_fold_to_their_ascii_spelling(string input, string expected)
    {
        SprAsciiText.Fold(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("Peña", "Pena")]
    [InlineData("Café", "Cafe")]
    [InlineData("Müller", "Muller")]
    [InlineData("Åkesson", "Akesson")]
    [InlineData("François", "Francois")]
    public void Accented_letters_keep_their_base_letter(string input, string expected)
    {
        // Transliteration, not deletion: these land on a shipping label, and "Pena" is a usable
        // degradation of the original where "Pea" is not.
        SprAsciiText.Fold(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("ß", "ss")]
    [InlineData("æ", "ae")]
    [InlineData("ø", "o")]
    [InlineData("ł", "l")]
    public void Letters_decomposition_cannot_handle_are_mapped_explicitly(string input, string expected)
    {
        SprAsciiText.Fold(input).Should().Be(expected);
    }

    [Fact]
    public void A_separator_is_not_swallowed()
    {
        // The failure mode if an em dash were dropped rather than mapped: the halves would run
        // together and the label would read "Customer1  Attn".
        SprAsciiText.Fold("A" + EmDash + "B").Should().Be("A-B");
    }

    [Fact]
    public void Zero_width_characters_are_removed_not_spaced()
    {
        SprAsciiText.Fold("AB\u200BCD").Should().Be("ABCD");
    }

    [Fact]
    public void Control_characters_illegal_in_xml_are_dropped()
    {
        // A second way a document could be refused, closed on the way past: U+0001 is not a legal
        // XML 1.0 character at all.
        SprAsciiText.Fold("AB\u0001C").Should().Be("ABC");
    }

    [Fact]
    public void Xml_legal_whitespace_survives()
    {
        SprAsciiText.Fold("line1\nline2\tend").Should().Be("line1\nline2\tend");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Null_and_empty_pass_through(string? value)
    {
        SprAsciiText.Fold(value!).Should().Be(value);
    }

    // ---- the case that must fail loudly instead ----

    [Theory]
    [InlineData("张三")]                   // Chinese
    [InlineData("Иван")]       // Cyrillic
    [InlineData("محمد")]       // Arabic
    public void A_wholly_non_ascii_name_is_reported_rather_than_silently_emptied(string value)
    {
        // No ASCII rendering worth inventing. FirstName is use="required" in SPR's schema, so the
        // generator must refuse the order locally rather than send an empty required attribute
        // and collect an unreadable rejection three minutes later.
        SprAsciiText.Fold(value).Should().BeEmpty();
        SprAsciiText.FoldsToNothing(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("John Smith")]
    [InlineData("Peña")]
    [InlineData("Customer1 — Attn: John Smith")]
    public void A_name_with_any_ascii_content_is_not_reported(string value)
    {
        SprAsciiText.FoldsToNothing(value).Should().BeFalse();
    }

    [Fact]
    public void An_absent_value_is_not_reported_as_unfoldable()
    {
        // Missing and unrepresentable are different errors; only the second belongs to this check.
        SprAsciiText.FoldsToNothing(null).Should().BeFalse();
        SprAsciiText.FoldsToNothing("").Should().BeFalse();
        SprAsciiText.FoldsToNothing("   ").Should().BeFalse();
    }
}
