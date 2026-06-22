using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

/// <summary>
/// Hand-built SOAP client for SPR's interactive web services (RPC/encoded SOAP 1.1 over the PHP
/// endpoints). Two request shapes are used by SPR: an &lt;input&gt;-wrapped form namespaced to
/// "{base}{Service}.php?wsdl" (Stock Check, Dealer Stock Check), and a flat "tempuri.org" form
/// (Quick Check Plus). Responses are parsed by local element name (namespace-agnostic).
/// </summary>
public class SprInteractiveServicesClient : ISprInteractiveServices
{
    private const string SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private const string SoapEncodingNs = "http://schemas.xmlsoap.org/soap/encoding/";
    private const string QuickCheckPlusNs = "http://tempuri.org/quick_check_plus/message";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SprInteractiveServicesClient> _logger;

    public SprInteractiveServicesClient(HttpClient httpClient, ILogger<SprInteractiveServicesClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SprPingResult> PingAsync(SprWebServiceConfig config, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            // Heartbeat = StockCheck with Action="?" (generic heartbeat response per the guide).
            var fields = BuildStockCheckInputFields(config, new SprStockCheckQuery(), action: "?");
            var envelope = BuildInputEnvelope("StockCheck", WsdlNs(config, "StockCheck"), fields);
            var xml = await PostAsync(config, "StockCheck", SoapAction(config, "StockCheck"), envelope, cancellationToken);
            sw.Stop();

            var doc = XDocument.Parse(xml);
            var fault = FindFault(doc);
            return new SprPingResult
            {
                Success = fault is null,
                Message = fault ?? Value(doc.Root, "RtnMessage") ?? "OK",
                ResponseTime = sw.Elapsed
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "SPR web-service ping failed against {BaseUrl}", config.BaseUrl);
            return new SprPingResult { Success = false, Message = ex.Message, ResponseTime = sw.Elapsed };
        }
    }

    public Task<SprStockCheckResult> StockCheckAsync(SprWebServiceConfig config, SprStockCheckQuery query, CancellationToken cancellationToken = default) =>
        ExecuteInputStyleAsync(config, "StockCheck", query, cancellationToken);

    public Task<SprStockCheckResult> DealerStockCheckAsync(SprWebServiceConfig config, SprStockCheckQuery query, CancellationToken cancellationToken = default) =>
        ExecuteInputStyleAsync(config, "DealerStockCheck", query, cancellationToken);

    public async Task<SprStockCheckResult> QuickCheckPlusAsync(SprWebServiceConfig config, SprStockCheckQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            var fields = new StringBuilder();
            AppendAuthFields(fields, config, action: "F");
            fields.Append($"<CustNumber>{Esc(config.CustNumber)}</CustNumber>");
            fields.Append($"<ItemNumber>{Esc(query.ItemNumber)}</ItemNumber>");
            fields.Append($"<MinInFullPacks>{(query.MinInFullPacks ? "Y" : "")}</MinInFullPacks>");
            for (var i = 0; i < 8; i++)
            {
                var dc = i < query.DcNumbers.Count ? query.DcNumbers[i].ToString("D3") : "";
                fields.Append($"<DcNumber{i + 1}>{dc}</DcNumber{i + 1}>");
            }

            var envelope = BuildMessageEnvelope("QuickCheckPlus", QuickCheckPlusNs, fields.ToString());
            var xml = await PostAsync(config, "QuickCheckPlus", soapAction: "", envelope, cancellationToken);

            var doc = XDocument.Parse(xml);
            var result = ParseItemFields(doc);
            // Quick Check Plus flattens DCs into Location1..8 slots.
            for (var i = 1; i <= 8; i++)
            {
                var dcNum = Value(doc.Root, $"Location{i}DcNum");
                if (string.IsNullOrWhiteSpace(dcNum)) continue;
                result.Dcs.Add(new SprDcStock
                {
                    DcNumber = dcNum,
                    DcName = Value(doc.Root, $"Location{i}DcName"),
                    Available = ParseInt(Value(doc.Root, $"Location{i}Available")) ?? 0,
                    Uom = Value(doc.Root, $"Location{i}SellUom")
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SPR Quick Check Plus failed for item {Item}", query.ItemNumber);
            return new SprStockCheckResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SprFreightResult> FindFreightRatesAsync(SprWebServiceConfig config, SprFreightQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            // Find Freight Rate uses the <input>-wrapped style with its own field names.
            var sb = new StringBuilder();
            AppendAuthFields(sb, config, action: "F");
            sb.Append($"<CustNumber>{Esc(config.CustNumber)}</CustNumber>");
            sb.Append($"<Warehouse>{query.ShipFromDc:D3}</Warehouse>");
            sb.Append($"<State>{Esc(query.State)}</State>");
            sb.Append($"<ZipCode>{Esc(query.PostalCode)}</ZipCode>");
            sb.Append($"<Weight>{query.Weight.ToString(CultureInfo.InvariantCulture)}</Weight>");
            sb.Append($"<Carrier>{Esc(query.Carrier)}</Carrier>");
            sb.Append($"<Service>{Esc(query.ServiceLevel)}</Service>");
            sb.Append($"<Residential>{(query.Residential ? "Y" : "N")}</Residential>");

            var envelope = BuildInputEnvelope("FindFreightRate", WsdlNs(config, "FindFreightRate"), sb.ToString());
            var xml = await PostAsync(config, "FindFreightRate", $"{SoapAction(config, "FindFreightRate")}#FindFreightRate", envelope, cancellationToken);

            var doc = XDocument.Parse(xml);
            var result = NewFreightResult(doc);
            foreach (var item in doc.Descendants().Where(e => e.Name.LocalName == "item"))
            {
                result.Rates.Add(new SprFreightRate
                {
                    ShipFromDc = Value(item, "WhseOut"),
                    Carrier = Value(item, "CarrOut"),
                    CarrierDescription = Value(item, "CarrierDesc"),
                    ShipVia = Value(item, "ShipVia"),
                    Rate = ParseDecimal(Value(item, "Rate")),
                    DeliveryDays = ParseInt(Value(item, "DeliveryDays")),
                    NumberOfCartons = ParseInt(Value(item, "NumberCartons")),
                    ServiceLevel = Value(item, "SrvLevOut"),
                    Residential = string.Equals(Value(item, "ResAdrInd"), "Y", StringComparison.OrdinalIgnoreCase)
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SPR Find Freight Rates failed (DC {Dc} -> {State} {Zip})", query.ShipFromDc, query.State, query.PostalCode);
            return new SprFreightResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    public async Task<SprFreightResult> LowestFreightRateAsync(SprWebServiceConfig config, SprFreightQuery query, CancellationToken cancellationToken = default)
    {
        try
        {
            // Lowest Freight Rate uses the tempuri "message" style with different field names.
            var sb = new StringBuilder();
            AppendAuthFields(sb, config, action: "F");
            sb.Append($"<CustNumber>{Esc(config.CustNumber)}</CustNumber>");
            sb.Append($"<ShipFromDc>{query.ShipFromDc:D3}</ShipFromDc>");
            sb.Append($"<StateCode>{Esc(query.State)}</StateCode>");
            sb.Append($"<PostalCode>{Esc(query.PostalCode)}</PostalCode>");
            sb.Append($"<TotWeight>{query.Weight.ToString(CultureInfo.InvariantCulture)}</TotWeight>");
            sb.Append($"<ReqCarrier>{Esc(query.Carrier)}</ReqCarrier>");
            sb.Append($"<ReqSrvcLevel>{Esc(query.ServiceLevel)}</ReqSrvcLevel>");
            sb.Append($"<Residential>{(query.Residential ? "Y" : "N")}</Residential>");

            var envelope = BuildMessageEnvelope("LowestFreightRate", "http://tempuri.org/lowest_freight_rate2/message", sb.ToString());
            var xml = await PostAsync(config, "LowestFreightRate", soapAction: "", envelope, cancellationToken);

            var doc = XDocument.Parse(xml);
            var result = NewFreightResult(doc);
            if (result.Success)
            {
                result.Rates.Add(new SprFreightRate
                {
                    ShipFromDc = Value(doc.Root, "DcNumber"),
                    Carrier = Value(doc.Root, "CarrierId"),
                    CarrierDescription = Value(doc.Root, "CarrierDesc"),
                    ShipVia = Value(doc.Root, "ShipVia"),
                    Rate = ParseDecimal(Value(doc.Root, "FrghtRate")),
                    DeliveryDays = ParseInt(Value(doc.Root, "DeliveryDays")),
                    NumberOfCartons = ParseInt(Value(doc.Root, "NumCartons")),
                    ServiceLevel = Value(doc.Root, "ServiceLevel"),
                    Residential = string.Equals(Value(doc.Root, "ResAdrInd"), "Y", StringComparison.OrdinalIgnoreCase)
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SPR Lowest Freight Rate failed (DC {Dc} -> {State} {Zip})", query.ShipFromDc, query.State, query.PostalCode);
            return new SprFreightResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    private static SprFreightResult NewFreightResult(XDocument doc)
    {
        var fault = FindFault(doc);
        var rtnStatus = Value(doc.Root, "RtnStatus");
        var rtnMessage = Value(doc.Root, "RtnMessage");
        var errorMessage = Value(doc.Root, "ErrorMessage");
        return new SprFreightResult
        {
            Success = fault is null && string.IsNullOrEmpty(errorMessage)
                && (rtnStatus == "0000" || (rtnStatus is null && string.Equals(rtnMessage, "OK", StringComparison.OrdinalIgnoreCase))),
            RtnStatus = rtnStatus,
            RtnMessage = rtnMessage,
            ErrorMessage = fault ?? errorMessage
        };
    }

    private async Task<SprStockCheckResult> ExecuteInputStyleAsync(
        SprWebServiceConfig config, string method, SprStockCheckQuery query, CancellationToken cancellationToken)
    {
        try
        {
            var fields = BuildStockCheckInputFields(config, query, action: "F");
            var envelope = BuildInputEnvelope(method, WsdlNs(config, method), fields);
            var xml = await PostAsync(config, method, SoapAction(config, method), envelope, cancellationToken);

            var doc = XDocument.Parse(xml);
            var result = ParseItemFields(doc);
            // Per-DC availability is a SOAP-ENC array <ResultsRows> of per-DC rows. The row element is
            // named per service (DealerStockCheckRow / StockCheckRow per the WSDL), so iterate the
            // array element's children rather than matching a specific row name.
            var resultsRows = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "ResultsRows");
            if (resultsRows != null)
            {
                foreach (var row in resultsRows.Elements())
                {
                    result.Dcs.Add(new SprDcStock
                    {
                        DcNumber = Value(row, "DcNum") ?? string.Empty,
                        DcName = Value(row, "DcName"),
                        Available = ParseInt(Value(row, "Available")) ?? 0,
                        Uom = Value(row, "Uom"),
                        OnOrder = ParseInt(Value(row, "OnOrder")),
                        Expected = Value(row, "Expected"),
                        Sprinter = string.Equals(Value(row, "Sprinter"), "Y", StringComparison.OrdinalIgnoreCase),
                        CutOff = Value(row, "CutOff"),
                        LeadTime = Value(row, "LeadTime"),
                        DcType = Value(row, "DcType")
                    });
                }
            }
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SPR {Method} failed for item {Item}", method, query.ItemNumber);
            return new SprStockCheckResult { Success = false, ErrorMessage = ex.Message };
        }
    }

    /// <summary>Parses the shared item-level fields + status from a stock-check response.</summary>
    private static SprStockCheckResult ParseItemFields(XDocument doc)
    {
        var fault = FindFault(doc);
        var rtnStatus = Value(doc.Root, "RtnStatus");
        var rtnMessage = Value(doc.Root, "RtnMessage");
        var errorMessage = Value(doc.Root, "ErrorMessage");

        var success = fault is null
            && string.IsNullOrEmpty(errorMessage)
            && (rtnStatus == "0000" || (rtnStatus is null && string.Equals(rtnMessage, "OK", StringComparison.OrdinalIgnoreCase)));

        return new SprStockCheckResult
        {
            Success = success,
            RtnStatus = rtnStatus,
            RtnMessage = rtnMessage,
            ErrorMessage = fault ?? errorMessage,
            SprItemNumber = Value(doc.Root, "SprItemNum"),
            StripNumber = Value(doc.Root, "StripNumber"),
            Upc = Value(doc.Root, "UpcNumber"),
            ItemStatus = Value(doc.Root, "ItemStatus"),
            Description = Value(doc.Root, "Description"),
            SellUom = Value(doc.Root, "SellUom"),
            OrderMinimum = ParseInt(Value(doc.Root, "OrderMinimum")),
            RetailPrice = ParseDecimal(Value(doc.Root, "RetailPrice")),
            RetailUom = Value(doc.Root, "RetailUom"),
            HazmatMessage = Value(doc.Root, "HazmatMessage"),
            UpchargeMessage = Value(doc.Root, "UpchargeMessage"),
            DealerPrice = ParseDecimal(Value(doc.Root, "DealerPrice")),
            Discountable = ParseYesNo(Value(doc.Root, "Discountable")),
            PriceDescription = Value(doc.Root, "PriceDescription"),
            TariffMessage = Value(doc.Root, "TariffMessage")
        };
    }

    private static string BuildStockCheckInputFields(SprWebServiceConfig config, SprStockCheckQuery query, string action)
    {
        var sb = new StringBuilder();
        AppendAuthFields(sb, config, action);
        sb.Append($"<CustNumber>{Esc(config.CustNumber)}</CustNumber>");
        // Single optional DC filter; empty = all DCs.
        var dc = query.DcNumbers.Count == 1 ? query.DcNumbers[0].ToString("D3") : "";
        sb.Append($"<DcNumber>{dc}</DcNumber>");
        sb.Append($"<ItemNumber>{Esc(query.ItemNumber)}</ItemNumber>");
        sb.Append($"<SortBy>{(query.SortBy == 'N' ? 'N' : 'A')}</SortBy>");
        sb.Append($"<MinInFullPacks>{(query.MinInFullPacks ? "Y" : "")}</MinInFullPacks>");
        sb.Append($"<AvailableOnly>{(query.AvailableOnly ? "Y" : "N")}</AvailableOnly>");
        return sb.ToString();
    }

    private static void AppendAuthFields(StringBuilder sb, SprWebServiceConfig config, string action)
    {
        sb.Append($"<GroupCode>{Esc(config.GroupCode)}</GroupCode>");
        sb.Append($"<UserID>{Esc(config.UserId)}</UserID>");
        sb.Append($"<Password>{Esc(config.Password)}</Password>");
        sb.Append($"<Action>{Esc(action)}</Action>");
    }

    // SPR's services are SOAP 1.1 rpc/encoded (NuSOAP). An empty <soapenv:Header/> element MUST be
    // OMITTED: when present, the NuSOAP server fails to decode the body parameters — every field
    // (incl. Action) arrives null and SPR rejects with RtnStatus 0009 "Invalid Service Action
    // Request Code." The method element also carries soapenv:encodingStyle, the correct marker for
    // rpc/encoded. (Both verified against the sprwstst test endpoint, 2026-06-21.)
    private static string BuildInputEnvelope(string method, string methodNs, string fieldsXml) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="{SoapNs}" xmlns:svc="{methodNs}">
          <soapenv:Body>
            <svc:{method} soapenv:encodingStyle="{SoapEncodingNs}"><input>{fieldsXml}</input></svc:{method}>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private static string BuildMessageEnvelope(string method, string methodNs, string fieldsXml) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <soapenv:Envelope xmlns:soapenv="{SoapNs}" xmlns:mes="{methodNs}">
          <soapenv:Body>
            <mes:{method} soapenv:encodingStyle="{SoapEncodingNs}">{fieldsXml}</mes:{method}>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private async Task<string> PostAsync(SprWebServiceConfig config, string service, string soapAction, string envelope, CancellationToken cancellationToken)
    {
        var url = $"{config.BaseUrl.TrimEnd('/')}/{service}.php";
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");

        _logger.LogDebug(
            "SPR {Service} request: POST {Url} (SOAPAction=\"{SoapAction}\")\n{Body}",
            service, url, soapAction, MaskSecrets(envelope));

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds <= 0 ? 30 : config.TimeoutSeconds));

        var response = await _httpClient.SendAsync(request, cts.Token);
        var content = await response.Content.ReadAsStringAsync(cts.Token);
        if (!response.IsSuccessStatusCode)
            _logger.LogWarning("SPR {Service} returned HTTP {Status}", service, (int)response.StatusCode);
        _logger.LogDebug("SPR {Service} raw response: {Body}", service, content);
        return content;
    }

    private static string WsdlNs(SprWebServiceConfig config, string service) =>
        $"{config.BaseUrl.TrimEnd('/')}/{service}.php?wsdl";

    private static string SoapAction(SprWebServiceConfig config, string service) => WsdlNs(config, service);

    private static string? FindFault(XDocument doc)
    {
        var fault = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "Fault");
        if (fault is null) return null;
        return Value(fault, "faultstring") ?? "SOAP fault";
    }

    private static string? Value(XElement? root, string localName)
    {
        var el = root?.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
        var v = el?.Value?.Trim();
        return string.IsNullOrEmpty(v) ? null : v;
    }

    private static int? ParseInt(string? s) =>
        int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static decimal? ParseDecimal(string? s) =>
        decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static bool? ParseYesNo(string? s) =>
        string.IsNullOrEmpty(s) ? null : string.Equals(s, "Y", StringComparison.OrdinalIgnoreCase);

    private static string Esc(string? value) =>
        string.IsNullOrEmpty(value) ? string.Empty : System.Security.SecurityElement.Escape(value);

    /// <summary>Redacts the &lt;Password&gt; element contents so the envelope is safe to log.</summary>
    private static string MaskSecrets(string envelope) =>
        Regex.Replace(envelope, "(<Password>)(.*?)(</Password>)", "$1***$3",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);
}
