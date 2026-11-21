using System.Globalization;
using System.Text;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;

/// <summary>
/// Folds outbound SPR text to printable ASCII.
/// </summary>
/// <remarks>
/// SPR's EZPO4 intake is ASCII-only in practice, whatever the XML declaration says. Their
/// translator (a Sterling/Gentran map) reads the file as a single-byte character set, so any byte
/// of 0x80 or above fails it with "Invalid byte 1 of 1-byte UTF-8 sequence" — reported back to us,
/// unhelpfully, as "XML Schema Validation Failed".
///
/// This is not a defect in what we produce. We write well-formed XML as valid UTF-8 with no BOM,
/// and it passes SPR's own XSD. Two orders on 2026-11-21 were rejected over a single em dash in a
/// ship-to name, while a structurally identical order on 2026-11-02 was accepted because it
/// happened to be pure ASCII. The constraint is theirs; the accommodation has to be ours.
///
/// Transliterate rather than strip wherever a sensible ASCII equivalent exists. These values end
/// up on a warehouse label, so "Pena" is a usable degradation of "Peña" and "Pea" is not.
/// </remarks>
public static class SprAsciiText
{
    /// <summary>
    /// Characters with a deliberate ASCII equivalent, applied before accent folding.
    /// </summary>
    /// <remarks>
    /// Unicode decomposition does not help with any of these — an em dash has no ASCII base
    /// character to decompose to, so without an explicit mapping it would simply be dropped and
    /// "Customer1 — Attn" would run together as "Customer1  Attn".
    /// </remarks>
    private static readonly Dictionary<char, string> Replacements = new()
    {
        // Dashes. The em dash is what rejected the 2026-11-21 orders.
        ['‐'] = "-", ['‑'] = "-", ['‒'] = "-", ['–'] = "-",
        ['—'] = "-", ['―'] = "-", ['−'] = "-",

        // Quotes, the usual damage from a value pasted out of Word.
        ['‘'] = "'", ['’'] = "'", ['‚'] = "'", ['‛'] = "'",
        ['“'] = "\"", ['”'] = "\"", ['„'] = "\"", ['‟'] = "\"",
        ['′'] = "'", ['″'] = "\"",

        // Spaces that are not a space.
        [' '] = " ", [' '] = " ", [' '] = " ", [' '] = " ",
        [' '] = " ", [' '] = " ", [' '] = " ", ['　'] = " ",

        // Zero-width characters: removed outright, never turned into a space.
        ['​'] = "", ['‌'] = "", ['‍'] = "", ['﻿'] = "",

        // Punctuation and symbols with a conventional ASCII spelling.
        ['…'] = "...", ['•'] = "-", ['·'] = "-",
        ['«'] = "\"", ['»'] = "\"",
        ['©'] = "(C)", ['®'] = "(R)", ['™'] = "(TM)",
        ['×'] = "x", ['÷'] = "/", ['⁄'] = "/",
        ['¼'] = "1/4", ['½'] = "1/2", ['¾'] = "3/4",
        ['°'] = " deg", ['℃'] = "C", ['℉'] = "F",

        // Ligatures and letters that decomposition leaves alone.
        ['Æ'] = "AE", ['æ'] = "ae", ['Œ'] = "OE", ['œ'] = "oe",
        ['Ø'] = "O", ['ø'] = "o", ['ß'] = "ss",
        ['Ð'] = "D", ['ð'] = "d", ['Þ'] = "Th", ['þ'] = "th",
        ['Ł'] = "L", ['ł'] = "l",

        // Currency: the symbol is worth keeping as a word when the amount is in text.
        ['€'] = "EUR", ['£'] = "GBP", ['¥'] = "JPY", ['¢'] = "c"
    };

    /// <summary>
    /// Returns <paramref name="value"/> reduced to printable ASCII, or the value unchanged when it
    /// already is. Null and empty pass through untouched.
    /// </summary>
    public static string Fold(string value)
    {
        if (string.IsNullOrEmpty(value) || IsPlainAscii(value))
            return value;

        var mapped = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (Replacements.TryGetValue(ch, out var replacement))
                mapped.Append(replacement);
            else
                mapped.Append(ch);
        }

        // Decomposition splits "é" into "e" + a combining acute, so dropping the combining marks
        // leaves the base letter. This is what turns "Peña" into "Pena".
        var decomposed = mapped.ToString().Normalize(NormalizationForm.FormD);

        var result = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;

            if (IsAllowed(ch))
                result.Append(ch);

            // Anything still outside ASCII after mapping and decomposition is dropped. A CJK or
            // Cyrillic name has no ASCII rendering we could invent that would be an improvement
            // on leaving it out, and a wrong guess on a shipping label is worse than a short one.
        }

        return result.ToString();
    }

    /// <summary>
    /// True when folding would leave nothing — the value is entirely non-ASCII.
    /// </summary>
    /// <remarks>
    /// Callers use this on fields SPR marks required, so the order fails here with a readable
    /// message instead of being sent with an empty required attribute and rejected three minutes
    /// later by a parser error nobody can read.
    /// </remarks>
    public static bool FoldsToNothing(string? value)
        => !string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(Fold(value));

    private static bool IsPlainAscii(string value)
    {
        foreach (var ch in value)
        {
            if (!IsAllowed(ch))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Printable ASCII, plus the three whitespace characters XML 1.0 permits. Other control
    /// characters are dropped: they are illegal in XML anyway, so this closes a second way a
    /// document could be refused.
    /// </summary>
    private static bool IsAllowed(char ch)
        => ch is >= ' ' and <= '~' || ch is '\t' or '\n' or '\r';
}
