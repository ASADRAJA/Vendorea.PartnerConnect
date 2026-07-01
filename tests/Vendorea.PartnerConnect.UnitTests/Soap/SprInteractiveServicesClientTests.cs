using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;
using Xunit;

namespace Vendorea.PartnerConnect.UnitTests.Soap;

public class SprInteractiveServicesClientTests
{
    private static SprWebServiceConfig Config() => new()
    {
        BaseUrl = "http://test.sprws/sprws/",
        GroupCode = "GRP",
        UserId = "WebService",
        Password = "secret",
        CustNumber = "CUST123"
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _response;
        public string? CapturedBody { get; private set; }
        public string? CapturedUrl { get; private set; }
        public StubHandler(string response) => _response = response;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedUrl = request.RequestUri!.ToString();
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_response, Encoding.UTF8, "text/xml")
            };
        }
    }

    private static (SprInteractiveServicesClient Client, StubHandler Handler) Build(string response)
    {
        var handler = new StubHandler(response);
        var http = new HttpClient(handler);
        return (new SprInteractiveServicesClient(http, NullLogger<SprInteractiveServicesClient>.Instance), handler);
    }

    private const string StockCheckResponse = """
        <?xml version="1.0" encoding="UTF-8"?>
        <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:tns="http://test.sprws/sprws/StockCheck.php?wsdl">
          <SOAP-ENV:Body>
            <ns1:StockCheckResponse xmlns:ns1="http://test.sprws/sprws/StockCheck.php?wsdl">
              <return xsi:type="tns:StockCheckResults">
                <ErrorMessage xsi:type="xsd:string"/>
                <RtnError xsi:nil="true"/>
                <RtnMessage xsi:type="xsd:string">OK</RtnMessage>
                <SprItemNum xsi:type="xsd:string">SPRW1011</SprItemNum>
                <StripNumber xsi:type="xsd:string">SPRW1011</StripNumber>
                <UpcNumber xsi:type="xsd:string">035255004503</UpcNumber>
                <ItemStatus xsi:type="xsd:string">A</ItemStatus>
                <Description xsi:type="xsd:string">PAD,LEGAL,LTR SZ,WE</Description>
                <SellUom xsi:type="xsd:string">EA</SellUom>
                <RetailPrice xsi:type="xsd:string">3.32</RetailPrice>
                <RetailUom xsi:type="xsd:string">EA</RetailUom>
                <OrderMinimum xsi:type="xsd:string">1</OrderMinimum>
                <HazmatMessage xsi:type="xsd:string">No</HazmatMessage>
                <ResultsRows SOAP-ENC:arrayType="tns:StockCheckRow[2]" xmlns:SOAP-ENC="http://schemas.xmlsoap.org/soap/encoding/">
                  <item xsi:type="tns:StockCheckRow">
                    <DcNum xsi:type="xsd:string">01</DcNum>
                    <DcName xsi:type="xsd:string">ATLANTA</DcName>
                    <Available xsi:type="xsd:string">101</Available>
                    <Uom xsi:type="xsd:string">EA</Uom>
                    <OnOrder xsi:type="xsd:string">288</OnOrder>
                    <Expected xsi:type="xsd:string">DUE</Expected>
                    <Sprinter xsi:type="xsd:string">Y</Sprinter>
                    <CutOff xsi:type="xsd:string">05:00 pm</CutOff>
                    <LeadTime xsi:type="xsd:string">1</LeadTime>
                    <DcType xsi:type="xsd:string">ALL</DcType>
                  </item>
                  <item xsi:type="tns:StockCheckRow">
                    <DcNum xsi:type="xsd:string">19</DcNum>
                    <DcName xsi:type="xsd:string">BALTIMORE</DcName>
                    <Available xsi:type="xsd:string">434</Available>
                    <Uom xsi:type="xsd:string">EA</Uom>
                    <OnOrder xsi:type="xsd:string">0</OnOrder>
                    <Sprinter xsi:type="xsd:string"/>
                    <DcType xsi:type="xsd:string">ALL</DcType>
                  </item>
                </ResultsRows>
              </return>
            </ns1:StockCheckResponse>
          </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

    private const string QuickCheckPlusResponse = """
        <?xml version="1.0" encoding="ISO-8859-1"?>
        <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <SOAP-ENV:Body>
            <ns1:QuickCheckPlusResponse xmlns:ns1="http://tempuri.org/quick_check_plus/message">
              <RtnStatus xsi:type="xsd:string">0000</RtnStatus>
              <RtnMessage xsi:type="xsd:string">OK</RtnMessage>
              <SprItemNum xsi:type="xsd:string">SPRW1011</SprItemNum>
              <ItemStatus xsi:type="xsd:string">A</ItemStatus>
              <Description xsi:type="xsd:string">PAD,LEGAL,LTR SZ,WE</Description>
              <SellUom xsi:type="xsd:string">EA</SellUom>
              <RetailPrice xsi:type="xsd:string">3.89</RetailPrice>
              <OrderMinimum xsi:type="xsd:string">1</OrderMinimum>
              <DealerPrice xsi:type="xsd:string">0.99</DealerPrice>
              <PriceDescription xsi:type="xsd:string">SMART CHOICE CATALOG</PriceDescription>
              <Location1DcNum xsi:type="xsd:string">001</Location1DcNum>
              <Location1DcName xsi:type="xsd:string">ATLANTA</Location1DcName>
              <Location1Available xsi:type="xsd:string">101</Location1Available>
              <Location1SellUom xsi:type="xsd:string">EA</Location1SellUom>
              <Location2DcNum xsi:type="xsd:string">016</Location2DcNum>
              <Location2DcName xsi:type="xsd:string">BIRMINGHAM</Location2DcName>
              <Location2Available xsi:type="xsd:string">627</Location2Available>
              <Location2SellUom xsi:type="xsd:string">EA</Location2SellUom>
              <Location3DcNum xsi:type="xsd:string"/>
              <Location3DcName xsi:type="xsd:string"/>
            </ns1:QuickCheckPlusResponse>
          </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

    [Fact]
    public async Task StockCheck_ParsesItemAttributesAndDcRows()
    {
        var (client, _) = Build(StockCheckResponse);

        var result = await client.StockCheckAsync(Config(), new SprStockCheckQuery { ItemNumber = "SPRW1011" });

        result.Success.Should().BeTrue();
        result.SprItemNumber.Should().Be("SPRW1011");
        result.Upc.Should().Be("035255004503");
        result.Description.Should().Be("PAD,LEGAL,LTR SZ,WE");
        result.RetailPrice.Should().Be(3.32m);
        result.OrderMinimum.Should().Be(1);
        result.Dcs.Should().HaveCount(2);

        var atlanta = result.Dcs[0];
        atlanta.DcNumber.Should().Be("01");
        atlanta.DcName.Should().Be("ATLANTA");
        atlanta.Available.Should().Be(101);
        atlanta.OnOrder.Should().Be(288);
        atlanta.Sprinter.Should().BeTrue();
        atlanta.CutOff.Should().Be("05:00 pm");
        atlanta.LeadTime.Should().Be("1");

        result.Dcs[1].Sprinter.Should().BeFalse();
    }

    [Fact]
    public async Task StockCheck_RequestEnvelope_CarriesAuthAndItem()
    {
        var (client, handler) = Build(StockCheckResponse);

        await client.StockCheckAsync(Config(), new SprStockCheckQuery { ItemNumber = "SPRW1011" });

        // Endpoint URL still comes from BaseUrl (test host)...
        handler.CapturedUrl.Should().Be("http://test.sprws/sprws/StockCheck.php");
        // ...but the rpc/encoded struct must match SPR's verified request: canonical prod-host
        // method namespace, empty header, a typed <input> struct, and typed string members.
        handler.CapturedBody.Should().Contain("<soapenv:Header/>");
        handler.CapturedBody.Should().Contain("xmlns:svc=\"http://sprws.sprich.com/sprws/StockCheck.php?wsdl\"");
        handler.CapturedBody.Should().Contain("<input xsi:type=\"svc:StockCheckInputs\">");
        handler.CapturedBody.Should().Contain("<GroupCode xsi:type=\"xsd:string\">GRP</GroupCode>");
        handler.CapturedBody.Should().Contain("<UserID xsi:type=\"xsd:string\">WebService</UserID>");
        handler.CapturedBody.Should().Contain("<Password xsi:type=\"xsd:string\">secret</Password>");
        handler.CapturedBody.Should().Contain("<Action xsi:type=\"xsd:string\">F</Action>");
        handler.CapturedBody.Should().Contain("<ItemNumber xsi:type=\"xsd:string\">SPRW1011</ItemNumber>");
    }

    [Fact]
    public async Task DealerStockCheck_RequestEnvelope_UsesDealerInputsType()
    {
        var (client, handler) = Build(StockCheckResponse);

        await client.DealerStockCheckAsync(Config(), new SprStockCheckQuery { ItemNumber = "SPRW1011" });

        handler.CapturedUrl.Should().Be("http://test.sprws/sprws/DealerStockCheck.php");
        handler.CapturedBody.Should().Contain("<input xsi:type=\"svc:DealerStockCheckInputs\">");
        handler.CapturedBody.Should().Contain("xmlns:svc=\"http://sprws.sprich.com/sprws/DealerStockCheck.php?wsdl\"");
    }

    [Fact]
    public async Task QuickCheckPlus_ParsesDealerPriceAndLocations()
    {
        var (client, handler) = Build(QuickCheckPlusResponse);

        var result = await client.QuickCheckPlusAsync(Config(),
            new SprStockCheckQuery { ItemNumber = "SPRW1011", DcNumbers = new[] { 1, 16 } });

        result.Success.Should().BeTrue();
        result.RtnStatus.Should().Be("0000");
        result.DealerPrice.Should().Be(0.99m);
        result.PriceDescription.Should().Be("SMART CHOICE CATALOG");
        result.Dcs.Should().HaveCount(2); // empty Location3 slot ignored
        result.Dcs[0].DcNumber.Should().Be("001");
        result.Dcs[0].DcName.Should().Be("ATLANTA");
        result.Dcs[0].Available.Should().Be(101);
        result.Dcs[1].DcNumber.Should().Be("016");

        // Quick Check Plus sends DcNumber1..8 (zero-padded), not an <input> wrapper.
        handler.CapturedBody.Should().Contain("<DcNumber1>001</DcNumber1>");
        handler.CapturedBody.Should().Contain("<DcNumber2>016</DcNumber2>");
        handler.CapturedBody.Should().Contain("<mes:QuickCheckPlus");
    }

    private const string FindFreightResponse = """
        <?xml version="1.0" encoding="UTF-8"?>
        <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:tns="http://test.sprws/sprws/FindFreightRate.php?wsdl">
          <SOAP-ENV:Body>
            <ns1:FindFreightRateResponse xmlns:ns1="http://test.sprws/sprws/FindFreightRate.php?wsdl">
              <return xsi:type="tns:FindFreightRateResults">
                <ErrorMessage xsi:type="xsd:string"/>
                <RtnStatus xsi:type="xsd:string">0000</RtnStatus>
                <RtnMessage xsi:type="xsd:string">OK</RtnMessage>
                <ResultsRows SOAP-ENC:arrayType="tns:FindFreightRateRow[2]" xmlns:SOAP-ENC="http://schemas.xmlsoap.org/soap/encoding/">
                  <item xsi:type="tns:FindFreightRateRow">
                    <WhseOut xsi:type="xsd:string">08</WhseOut>
                    <CarrOut xsi:type="xsd:string">UPS</CarrOut>
                    <CarrierDesc xsi:type="xsd:string">UPS NEXT DAY AIR EARLY AM</CarrierDesc>
                    <ShipVia xsi:type="xsd:string">UPEA</ShipVia>
                    <Rate xsi:type="xsd:string">68.16</Rate>
                    <DeliveryDays xsi:type="xsd:string">1</DeliveryDays>
                    <NumberCartons xsi:type="xsd:string">1</NumberCartons>
                    <SrvLevOut xsi:type="xsd:string">2</SrvLevOut>
                    <ResAdrInd xsi:type="xsd:string">N</ResAdrInd>
                  </item>
                  <item xsi:type="tns:FindFreightRateRow">
                    <WhseOut xsi:type="xsd:string">08</WhseOut>
                    <CarrOut xsi:type="xsd:string">UPS</CarrOut>
                    <CarrierDesc xsi:type="xsd:string">UPS Next Day Air Bill SPR</CarrierDesc>
                    <ShipVia xsi:type="xsd:string">UPSN</ShipVia>
                    <Rate xsi:type="xsd:string">34.83</Rate>
                    <DeliveryDays xsi:type="xsd:string">1</DeliveryDays>
                    <NumberCartons xsi:type="xsd:string">1</NumberCartons>
                    <SrvLevOut xsi:type="xsd:string">1</SrvLevOut>
                    <ResAdrInd xsi:type="xsd:string">N</ResAdrInd>
                  </item>
                </ResultsRows>
              </return>
            </ns1:FindFreightRateResponse>
          </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

    private const string LowestFreightResponse = """
        <?xml version="1.0" encoding="ISO-8859-1"?>
        <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://schemas.xmlsoap.org/soap/envelope/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
          <SOAP-ENV:Body>
            <ns1:LowestFreightRateResponse xmlns:ns1="http://tempuri.org/lowest_freight_rate2/message">
              <ErrorMessage xsi:type="xsd:string"/>
              <RtnStatus xsi:type="xsd:string">0000</RtnStatus>
              <RtnMessage xsi:type="xsd:string">OK</RtnMessage>
              <DcNumber xsi:type="xsd:string">08</DcNumber>
              <CarrierId xsi:type="xsd:string">UPS</CarrierId>
              <CarrierDesc xsi:type="xsd:string">UPS NEXT DAY AIR SAVER</CarrierDesc>
              <ShipVia xsi:type="xsd:string">UP2S</ShipVia>
              <FrghtRate xsi:type="xsd:string">31.66</FrghtRate>
              <DeliveryDays xsi:type="xsd:string">1</DeliveryDays>
              <NumCartons xsi:type="xsd:string">1</NumCartons>
              <ServiceLevel xsi:type="xsd:string">5</ServiceLevel>
              <ResAdrInd xsi:type="xsd:string">N</ResAdrInd>
            </ns1:LowestFreightRateResponse>
          </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

    private static SprFreightQuery FreightQuery() => new()
    {
        ShipFromDc = 8, State = "GA", PostalCode = "30341", Weight = 1.0m, ServiceLevel = "9", Residential = false
    };

    [Fact]
    public async Task FindFreightRates_ParsesRateRows()
    {
        var (client, handler) = Build(FindFreightResponse);

        var result = await client.FindFreightRatesAsync(Config(), FreightQuery());

        result.Success.Should().BeTrue();
        result.Rates.Should().HaveCount(2);
        result.Rates[0].Carrier.Should().Be("UPS");
        result.Rates[0].ShipVia.Should().Be("UPEA");
        result.Rates[0].Rate.Should().Be(68.16m);
        result.Rates[0].DeliveryDays.Should().Be(1);
        result.Rates[1].Rate.Should().Be(34.83m);

        // Find Freight uses the <input> (rpc/encoded struct) style with typed Warehouse/State/ZipCode/Weight fields.
        handler.CapturedUrl.Should().Be("http://test.sprws/sprws/FindFreightRate.php");
        handler.CapturedBody.Should().Contain("<input xsi:type=\"svc:FindFreightRateInputs\">");
        handler.CapturedBody.Should().Contain("<Warehouse xsi:type=\"xsd:string\">008</Warehouse>");
        handler.CapturedBody.Should().Contain("<ZipCode xsi:type=\"xsd:string\">30341</ZipCode>");
    }

    [Fact]
    public async Task LowestFreightRate_ParsesSingleRate()
    {
        var (client, handler) = Build(LowestFreightResponse);

        var result = await client.LowestFreightRateAsync(Config(), FreightQuery());

        result.Success.Should().BeTrue();
        result.Rates.Should().ContainSingle();
        result.Rates[0].CarrierDescription.Should().Be("UPS NEXT DAY AIR SAVER");
        result.Rates[0].ShipVia.Should().Be("UP2S");
        result.Rates[0].Rate.Should().Be(31.66m);
        result.Rates[0].ServiceLevel.Should().Be("5");

        // Lowest uses the tempuri "message" style with ShipFromDc/StateCode/PostalCode/TotWeight.
        handler.CapturedBody.Should().Contain("<ShipFromDc>008</ShipFromDc>");
        handler.CapturedBody.Should().Contain("<TotWeight>1.0</TotWeight>");
        handler.CapturedBody.Should().Contain("<mes:LowestFreightRate");
    }
}
