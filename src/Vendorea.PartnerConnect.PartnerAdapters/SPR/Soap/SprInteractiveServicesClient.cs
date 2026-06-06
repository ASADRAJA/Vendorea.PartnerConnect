using System.Diagnostics;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Vendorea.PartnerConnect.PartnerAdapters.SPR.Soap;

/// <summary>
/// SOAP client for SPR interactive web services.
/// Provides real-time operations for status checks, inventory queries, and tracking.
///
/// NOTE: Order submission is NOT done via this client. Orders are documents
/// that flow through the document pipeline (XML generation, transport, processing).
/// </summary>
public class SprInteractiveServicesClient : ISprInteractiveServices
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SprInteractiveServicesClient> _logger;

    // SOAP envelope template
    private const string SoapEnvelopeTemplate = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""
               xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance""
               xmlns:xsd=""http://www.w3.org/2001/XMLSchema"">
    <soap:Header>
        <AuthHeader xmlns=""http://spr.com/webservices"">
            <Username>{0}</Username>
            <Password>{1}</Password>
            <EnterpriseCode>{2}</EnterpriseCode>
        </AuthHeader>
    </soap:Header>
    <soap:Body>
        {3}
    </soap:Body>
</soap:Envelope>";

    public SprInteractiveServicesClient(
        HttpClient httpClient,
        ILogger<SprInteractiveServicesClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<SprOrderStatusResult> GetOrderStatusAsync(
        string poNumber,
        SprSoapConfig config,
        CancellationToken cancellationToken = default)
    {
        var result = new SprOrderStatusResult { PoNumber = poNumber };

        try
        {
            _logger.LogDebug("Getting order status for PO {PoNumber}", poNumber);

            var soapBody = $@"<GetOrderStatus xmlns=""http://spr.com/webservices"">
                <customerPONumber>{EscapeXml(poNumber)}</customerPONumber>
                <buyerOrgCode>{EscapeXml(config.BuyerOrgCode)}</buyerOrgCode>
            </GetOrderStatus>";

            var soapEnvelope = string.Format(
                SoapEnvelopeTemplate,
                EscapeXml(config.Username),
                EscapeXml(config.Password),
                EscapeXml(config.EnterpriseCode),
                soapBody);

            var response = await SendSoapRequestAsync(
                config.EndpointUrl,
                "http://spr.com/webservices/GetOrderStatus",
                soapEnvelope,
                config.TimeoutSeconds,
                cancellationToken);

            ParseOrderStatusResponse(response, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting order status for PO {PoNumber}", poNumber);
            result.ErrorMessage = $"Status check failed: {ex.Message}";
        }

        return result;
    }

    public async Task<SprInventoryResult> GetInventoryAsync(
        IEnumerable<string> skus,
        SprSoapConfig config,
        CancellationToken cancellationToken = default)
    {
        var result = new SprInventoryResult();
        var skuList = skus.ToList();

        try
        {
            _logger.LogDebug("Getting inventory for {Count} SKUs", skuList.Count);

            var skuElements = string.Join("", skuList.Select(s =>
                $"<sku>{EscapeXml(s)}</sku>"));

            var soapBody = $@"<GetInventory xmlns=""http://spr.com/webservices"">
                <skuList>{skuElements}</skuList>
                <buyerOrgCode>{EscapeXml(config.BuyerOrgCode)}</buyerOrgCode>
            </GetInventory>";

            var soapEnvelope = string.Format(
                SoapEnvelopeTemplate,
                EscapeXml(config.Username),
                EscapeXml(config.Password),
                EscapeXml(config.EnterpriseCode),
                soapBody);

            var response = await SendSoapRequestAsync(
                config.EndpointUrl,
                "http://spr.com/webservices/GetInventory",
                soapEnvelope,
                config.TimeoutSeconds,
                cancellationToken);

            ParseInventoryResponse(response, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting inventory for {Count} SKUs", skuList.Count);
            result.ErrorMessage = $"Inventory check failed: {ex.Message}";
        }

        return result;
    }

    public async Task<SprTrackingResult> GetTrackingAsync(
        string trackingNumber,
        SprSoapConfig config,
        CancellationToken cancellationToken = default)
    {
        var result = new SprTrackingResult { TrackingNumber = trackingNumber };

        try
        {
            _logger.LogDebug("Getting tracking for {TrackingNumber}", trackingNumber);

            var soapBody = $@"<GetTracking xmlns=""http://spr.com/webservices"">
                <trackingNumber>{EscapeXml(trackingNumber)}</trackingNumber>
            </GetTracking>";

            var soapEnvelope = string.Format(
                SoapEnvelopeTemplate,
                EscapeXml(config.Username),
                EscapeXml(config.Password),
                EscapeXml(config.EnterpriseCode),
                soapBody);

            var response = await SendSoapRequestAsync(
                config.EndpointUrl,
                "http://spr.com/webservices/GetTracking",
                soapEnvelope,
                config.TimeoutSeconds,
                cancellationToken);

            ParseTrackingResponse(response, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tracking for {TrackingNumber}", trackingNumber);
            result.ErrorMessage = $"Tracking lookup failed: {ex.Message}";
        }

        return result;
    }

    public async Task<SprConnectionTestResult> TestConnectionAsync(
        SprSoapConfig config,
        CancellationToken cancellationToken = default)
    {
        var result = new SprConnectionTestResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Testing connection to SPR at {Endpoint}", config.EndpointUrl);

            var soapBody = @"<Ping xmlns=""http://spr.com/webservices"" />";

            var soapEnvelope = string.Format(
                SoapEnvelopeTemplate,
                EscapeXml(config.Username),
                EscapeXml(config.Password),
                EscapeXml(config.EnterpriseCode),
                soapBody);

            var response = await SendSoapRequestAsync(
                config.EndpointUrl,
                "http://spr.com/webservices/Ping",
                soapEnvelope,
                config.TimeoutSeconds,
                cancellationToken);

            stopwatch.Stop();
            result.ResponseTime = stopwatch.Elapsed;

            // Check for successful response
            if (!string.IsNullOrWhiteSpace(response))
            {
                result.Success = !response.Contains("Fault");
                if (!result.Success)
                {
                    result.ErrorMessage = ExtractFaultMessage(response);
                }
            }
            else
            {
                result.ErrorMessage = "Empty response from server";
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            result.ResponseTime = stopwatch.Elapsed;
            _logger.LogError(ex, "Connection test failed for {Endpoint}", config.EndpointUrl);
            result.ErrorMessage = $"Connection failed: {ex.Message}";
        }

        return result;
    }

    private async Task<string> SendSoapRequestAsync(
        string endpointUrl,
        string soapAction,
        string soapEnvelope,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, endpointUrl);
        request.Content = new StringContent(soapEnvelope, Encoding.UTF8, "text/xml");
        request.Headers.Add("SOAPAction", soapAction);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var response = await _httpClient.SendAsync(request, cts.Token);
        var content = await response.Content.ReadAsStringAsync(cts.Token);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "SOAP request to {Endpoint} returned {StatusCode}: {Content}",
                endpointUrl, response.StatusCode, content);
        }

        return content;
    }

    private static void ParseOrderStatusResponse(string responseXml, SprOrderStatusResult result)
    {
        try
        {
            var doc = XDocument.Parse(responseXml);

            var fault = doc.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "Fault");

            if (fault != null)
            {
                result.ErrorMessage = GetElementValue(fault, "faultstring");
                return;
            }

            var statusResponse = doc.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "GetOrderStatusResponse" ||
                e.Name.LocalName == "OrderStatus");

            if (statusResponse != null)
            {
                result.Success = true;
                result.PartnerOrderNumber = GetElementValue(statusResponse, "SONumber");
                result.OrderStatus = GetElementValue(statusResponse, "Status");
                result.ExpectedShipDate = ParseDate(GetElementValue(statusResponse, "ExpectedShipDate"));
                result.ActualShipDate = ParseDate(GetElementValue(statusResponse, "ShipDate"));
                result.TrackingNumber = GetElementValue(statusResponse, "TrackingNumber");

                // Parse lines
                var lines = statusResponse.Descendants().Where(e =>
                    e.Name.LocalName == "LineItem" ||
                    e.Name.LocalName == "OrderLine");

                foreach (var line in lines)
                {
                    result.Lines.Add(new SprOrderLineStatus
                    {
                        LineNumber = int.TryParse(GetElementValue(line, "LineNo"), out var ln) ? ln : 0,
                        Sku = GetElementValue(line, "ItemId") ?? string.Empty,
                        QuantityOrdered = int.TryParse(GetElementValue(line, "QtyOrdered"), out var qo) ? qo : 0,
                        QuantityShipped = int.TryParse(GetElementValue(line, "QtyShipped"), out var qs) ? qs : 0,
                        QuantityBackordered = int.TryParse(GetElementValue(line, "QtyBackordered"), out var qb) ? qb : 0,
                        Status = GetElementValue(line, "Status") ?? string.Empty
                    });
                }
            }
        }
        catch
        {
            result.ErrorMessage = "Failed to parse response XML";
        }
    }

    private static void ParseInventoryResponse(string responseXml, SprInventoryResult result)
    {
        try
        {
            var doc = XDocument.Parse(responseXml);

            var fault = doc.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "Fault");

            if (fault != null)
            {
                result.ErrorMessage = GetElementValue(fault, "faultstring");
                return;
            }

            var inventoryResponse = doc.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "GetInventoryResponse" ||
                e.Name.LocalName == "InventoryResult");

            if (inventoryResponse != null)
            {
                result.Success = true;

                var items = inventoryResponse.Descendants().Where(e =>
                    e.Name.LocalName == "Item" ||
                    e.Name.LocalName == "InventoryItem");

                foreach (var item in items)
                {
                    result.Items.Add(new SprInventoryItem
                    {
                        Sku = GetElementValue(item, "ItemId") ?? GetElementValue(item, "Sku") ?? string.Empty,
                        QuantityAvailable = int.TryParse(GetElementValue(item, "QtyAvailable"), out var qa) ? qa : 0,
                        QuantityOnOrder = int.TryParse(GetElementValue(item, "QtyOnOrder"), out var qo) ? qo : 0,
                        AvailabilityStatus = GetElementValue(item, "Status") ?? "Unknown",
                        ExpectedRestockDate = ParseDate(GetElementValue(item, "RestockDate")),
                        WarehouseCode = GetElementValue(item, "Warehouse")
                    });
                }
            }
        }
        catch
        {
            result.ErrorMessage = "Failed to parse response XML";
        }
    }

    private static void ParseTrackingResponse(string responseXml, SprTrackingResult result)
    {
        try
        {
            var doc = XDocument.Parse(responseXml);

            var fault = doc.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "Fault");

            if (fault != null)
            {
                result.ErrorMessage = GetElementValue(fault, "faultstring");
                return;
            }

            var trackingResponse = doc.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "GetTrackingResponse" ||
                e.Name.LocalName == "TrackingResult");

            if (trackingResponse != null)
            {
                result.Success = true;
                result.Carrier = GetElementValue(trackingResponse, "Carrier");
                result.ServiceLevel = GetElementValue(trackingResponse, "ServiceLevel");
                result.CurrentStatus = GetElementValue(trackingResponse, "Status");
                result.CurrentLocation = GetElementValue(trackingResponse, "Location");
                result.ShipDate = ParseDate(GetElementValue(trackingResponse, "ShipDate"));
                result.DeliveryDate = ParseDate(GetElementValue(trackingResponse, "DeliveryDate"));
                result.EstimatedDelivery = ParseDate(GetElementValue(trackingResponse, "EstimatedDelivery"));

                var events = trackingResponse.Descendants().Where(e =>
                    e.Name.LocalName == "Event" ||
                    e.Name.LocalName == "TrackingEvent");

                foreach (var evt in events)
                {
                    result.Events.Add(new SprTrackingEvent
                    {
                        Timestamp = ParseDate(GetElementValue(evt, "Timestamp")) ?? DateTime.UtcNow,
                        Status = GetElementValue(evt, "Status") ?? string.Empty,
                        Location = GetElementValue(evt, "Location"),
                        Description = GetElementValue(evt, "Description")
                    });
                }
            }
        }
        catch
        {
            result.ErrorMessage = "Failed to parse response XML";
        }
    }

    private static string ExtractFaultMessage(string responseXml)
    {
        try
        {
            var doc = XDocument.Parse(responseXml);
            var fault = doc.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "Fault");

            return GetElementValue(fault, "faultstring")
                ?? GetElementValue(fault, "detail")
                ?? "Unknown SOAP fault";
        }
        catch
        {
            return "Failed to parse fault response";
        }
    }

    private static string? GetElementValue(XElement? parent, string localName)
    {
        if (parent == null) return null;

        var element = parent.Descendants().FirstOrDefault(e =>
            e.Name.LocalName.Equals(localName, StringComparison.OrdinalIgnoreCase));

        return element?.Value?.Trim();
    }

    private static DateTime? ParseDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        if (DateTime.TryParse(dateStr, out var date))
            return date;

        return null;
    }

    private static string EscapeXml(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
