# Merchant360 Connector

HTTP client for communicating with the Merchant360 API to push processed partner data (prices, content, inventory) to merchants.

## Implementation Status: COMPLETE

The connector is **fully implemented** with all required functionality:

| Feature | Status | Description |
|---------|--------|-------------|
| Price Batch Push | ✅ | Push up to 10K price updates per batch per merchant |
| Content Batch Push | ✅ | Push product content (shared across merchants) |
| Category Batch Push | ✅ | Push category hierarchies |
| Order Status Updates | ✅ | Push POA/acknowledgment status |
| Shipment Updates | ✅ | Push ASN tracking information |
| Invoice Updates | ✅ | Push invoice data |
| Inventory Updates | ✅ | Push inventory levels (delta and full snapshot) |
| Subscription Management | ✅ | Full subscription workflow |

## Production Configuration Required

### 1. API Endpoint Configuration

Add to `appsettings.json`:

```json
{
  "Merchant360": {
    "BaseUrl": "https://api.merchant360.com",
    "ApiKey": "<from-key-vault>",
    "TimeoutSeconds": 30,
    "RetryCount": 3
  }
}
```

### 2. HttpClient Registration

Add to `Program.cs` or DI configuration:

```csharp
services.AddHttpClient<IMerchant360Client, Merchant360ApiClient>(client =>
{
    client.BaseAddress = new Uri(configuration["Merchant360:BaseUrl"]);
    client.DefaultRequestHeaders.Add("X-API-Key", configuration["Merchant360:ApiKey"]);
    client.Timeout = TimeSpan.FromSeconds(configuration.GetValue<int>("Merchant360:TimeoutSeconds", 30));
})
.AddPolicyHandler(GetRetryPolicy()); // Optional: Polly retry policy
```

### 3. API Key Management

Store API key in Azure Key Vault:

```bash
az keyvault secret set --vault-name <vault-name> --name "Merchant360-ApiKey" --value "<api-key>"
```

## API Contract

The connector implements the `IMerchant360Client` interface defined in `Vendorea.PartnerConnect.Contracts`.

### Expected M360 API Endpoints

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/health` | Connection test |
| GET | `/api/v1/partner-connect/merchants` | List active merchants |
| GET | `/api/v1/partner-connect/trading-partners` | List trading partners |
| POST | `/api/v1/partner-connect/merchants/{id}/prices/batch` | Push price batch |
| POST | `/api/v1/partner-connect/content/products/batch` | Push content batch |
| POST | `/api/v1/partner-connect/content/categories/batch` | Push category batch |
| POST | `/api/v1/partner-connect/merchants/{id}/orders/status` | Push order status |
| POST | `/api/v1/partner-connect/merchants/{id}/shipments` | Push shipment |
| POST | `/api/v1/partner-connect/merchants/{id}/invoices` | Push invoice |
| POST | `/api/v1/partner-connect/merchants/{id}/inventory` | Push inventory update |
| POST | `/api/v1/partner-connect/merchants/{id}/inventory/snapshot` | Push full inventory |
| GET/POST | `/api/v1/partner-connect/subscriptions/*` | Subscription management |

## Integration Testing

Before production deployment:

1. Obtain M360 sandbox credentials
2. Configure sandbox endpoint in `appsettings.Development.json`
3. Run integration tests against sandbox:

```bash
dotnet test --filter "Category=Merchant360Integration"
```

## Usage Example

```csharp
// Inject the client
public class PriceSyncService
{
    private readonly IMerchant360Client _m360Client;

    public async Task SyncPricesToMerchantAsync(int merchantId, List<PriceRecord> prices)
    {
        var request = new PriceBatchRequest
        {
            TradingPartnerId = 1,
            TradingPartnerCode = "SPR",
            Items = prices.Select(p => new PriceBatchItem
            {
                StockNumber = p.Sku,
                NetCost = p.DealerCost,
                RetailListPrice = p.ListPrice
            }).ToList()
        };

        var result = await _m360Client.PushPriceBatchAsync(merchantId, request);

        if (!result.Success)
        {
            _logger.LogError("Price sync failed: {Errors}", result.Errors);
        }
    }
}
```

## Error Handling

The client returns structured response objects with:
- `Success` flag
- `Errors` list for failures
- Detailed counts (created, updated, skipped)
- `SyncLogId` for traceability

All network/API errors are caught and logged; they don't throw exceptions.
