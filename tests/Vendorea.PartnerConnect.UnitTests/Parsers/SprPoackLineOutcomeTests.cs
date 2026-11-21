using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Xml;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Parsers;

/// <summary>
/// What SPR says about individual lines, and whether we hear it.
/// </summary>
/// <remarks>
/// SPR acknowledged WPO-20261121-0003 with PoAckStatus="A" while its only line came back
/// AckStatus="I" AckDesc="BAD STOCK #". Nothing would ever ship, and the order read as a clean
/// acknowledgement through PartnerConnect, the portal and the Merchant360 callback.
///
/// The parser already mapped 'I' to PoAckLineStatus.Error. The order-level check looked only for
/// a literal "E", so the file disagreed with itself about what an error was.
/// </remarks>
public class SprPoackLineOutcomeTests
{
    private readonly SprPoackParser _sut = new(NullLogger<SprPoackParser>.Instance);

    private static string Ack(string headerStatus, params (string Code, string Desc, string Sku)[] lines)
    {
        var lineXml = string.Empty;
        var n = 1;
        foreach (var (code, desc, sku) in lines)
        {
            lineXml +=
                $@"<OrderLine PrimeLineNo=""{n}""><OrderLineTranQuantity TransactionalUOM=""EA"" OrderedQty=""1"" />
                   <Item CustomerItem=""{sku}"" /><LinePriceInfo UnitPrice=""25.89"" />
                   <Extn><EXTNSprOrderLineList><EXTNSprOrderLine BuyerItemNo=""{sku}"" QtyShipped=""0""
                     QtyBackordered=""0"" AckStatus=""{code}"" AckDesc=""{desc}"" /></EXTNSprOrderLineList></Extn>
                   </OrderLine>";
            n++;
        }

        return $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes"" ?>
            <Order EnterpriseCode=""SPR"" BuyerOrganizationCode=""0033822.00"" OrderType=""03""
                   CustomerPONo=""WPO-20261121-0003"" OrderNo=""NOYPICKT"">
              <OrderLines>{lineXml}</OrderLines>
              <Extn><EXTNSprOrderHeaderList>
                <EXTNSprOrderHeader PoAckStatus=""{headerStatus}"" SprSoNum=""NOYPICKT"" />
              </EXTNSprOrderHeaderList></Extn>
            </Order>";
    }

    // ---- the acknowledgement that was misread ----

    [Fact]
    public void A_bad_stock_number_is_an_error_even_when_the_header_says_accepted()
    {
        // Verbatim from SPR's reply to WPO-20261121-0003.
        var result = _sut.Parse(Ack("A", ("I", "BAD STOCK #", "STCPXT515191410")), dealerId: 1);

        result.Success.Should().BeTrue();
        result.Result!.IsError.Should().BeTrue("every line was refused, so nothing will ship");
        result.Result.Status.Should().Be(PoAckStatus.Error);
    }

    [Fact]
    public void The_reason_SPR_gave_is_carried_rather_than_discarded()
    {
        var result = _sut.Parse(Ack("A", ("I", "BAD STOCK #", "STCPXT515191410")), dealerId: 1);

        result.Result!.ErrorMessage.Should().Contain("BAD STOCK #");
    }

    [Theory]
    [InlineData("E")]
    [InlineData("I")]
    [InlineData("Q")]
    [InlineData("U")]
    public void Every_input_error_code_SPR_documents_is_treated_as_one(string code)
    {
        // The enum's own documentation says 'E' plus 'I'/'Q'/'U'; only 'E' was acted on.
        var result = _sut.Parse(Ack("A", (code, "REFUSED", "SKU1")), dealerId: 1);

        result.Result!.IsError.Should().BeTrue();
        result.Result.Lines.Should().ContainSingle()
            .Which.Status.Should().Be(PoAckLineStatus.Error);
    }

    // ---- partial refusals ----

    [Fact]
    public void One_refused_line_among_accepted_ones_is_a_partial_acceptance()
    {
        // Not an error - the rest of the order ships - but not a clean acknowledgement either.
        var result = _sut.Parse(
            Ack("A", ("A", "OK", "GOODSKU"), ("I", "BAD STOCK #", "BADSKU")), dealerId: 1);

        result.Result!.IsError.Should().BeFalse();
        result.Result.Status.Should().Be(PoAckStatus.PartiallyAccepted);
        result.Result.ErrorMessage.Should().Contain("BAD STOCK #",
            "a partially refused order still has a reason somebody needs to see");
    }

    [Fact]
    public void Each_line_keeps_its_own_outcome()
    {
        var result = _sut.Parse(
            Ack("A", ("A", "OK", "GOODSKU"), ("I", "BAD STOCK #", "BADSKU")), dealerId: 1);

        var lines = result.Result!.Lines;
        lines.Should().HaveCount(2);
        lines[0].Status.Should().Be(PoAckLineStatus.Accepted);
        lines[1].Status.Should().Be(PoAckLineStatus.Error);
        lines[1].Notes.Should().Be("BAD STOCK #");
    }

    // ---- what must not change ----

    [Fact]
    public void A_fully_accepted_order_is_still_a_clean_acknowledgement()
    {
        // The control: the common case must be untouched by this.
        var result = _sut.Parse(Ack("A", ("A", "", "STCUSB3AUB50CMS")), dealerId: 1);

        result.Result!.IsError.Should().BeFalse();
        result.Result.Status.Should().Be(PoAckStatus.Accepted);
        result.Result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void A_header_error_is_still_an_error()
    {
        var result = _sut.Parse(Ack("E", ("E", "CANNOT PROCESS", "SKU1")), dealerId: 1);

        result.Result!.IsError.Should().BeTrue();
        result.Result.Status.Should().Be(PoAckStatus.Error);
    }

    [Fact]
    public void A_backordered_line_is_not_an_error()
    {
        var result = _sut.Parse(Ack("A", ("B", "BACKORDERED", "SKU1")), dealerId: 1);

        result.Result!.IsError.Should().BeFalse();
        result.Result.Lines.Should().ContainSingle()
            .Which.Status.Should().Be(PoAckLineStatus.Backordered);
    }
}
