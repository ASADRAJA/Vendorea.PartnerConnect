using FluentAssertions;
using Vendorea.PartnerConnect.Infrastructure.SprContent;
using Xunit;

namespace Vendorea.PartnerConnect.DatabaseTests;

/// <summary>
/// Dates parsed out of SPR's files must come back as UTC, because Npgsql rejects any DateTime
/// whose Kind is not Utc when it maps to timestamptz.
/// </summary>
/// <remarks>
/// This is the DateTime hazard that actually applies to PartnerConnect. Every "new DateTime(...)"
/// in src already passes DateTimeKind.Utc explicitly, so the obvious risk was already handled -
/// but the file parsers were not. DateTime.TryParse and TryParseExact with DateTimeStyles.None
/// both yield Unspecified, and AssumeUniversal on its own yields Local, because it converts to
/// local time rather than staying in UTC.
///
/// SQL Server's datetime2 carried no timezone so it stored whatever it was handed and none of
/// this mattered. On PostgreSQL every one of these values would have thrown on save, on paths
/// that only run when a real SPR file is processed - which is why the earlier probes missed it.
/// </remarks>
public class ParsedDateKindTests
{
    [Theory]
    [InlineData("2026-01-15")]
    [InlineData("2026-01-15T09:30:00")]
    [InlineData("01/15/2026")]
    public void Content_file_parser_returns_utc(string input)
    {
        var parsed = SprContentFileParser.ParseDate(input);

        parsed.Should().NotBeNull();
        parsed!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void Content_file_parser_preserves_the_wall_clock_value()
    {
        // The fix labels the parsed value as UTC rather than shifting it, so what lands in the
        // database is the same instant SQL Server stored.
        var parsed = SprContentFileParser.ParseDate("2026-01-15T09:30:00");

        parsed!.Value.Should().Be(new DateTime(2026, 1, 15, 9, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Blank_input_still_returns_null()
    {
        SprContentFileParser.ParseDate("").Should().BeNull();
        SprContentFileParser.ParseDate("   ").Should().BeNull();
        SprContentFileParser.ParseDate("not a date").Should().BeNull();
    }
}
