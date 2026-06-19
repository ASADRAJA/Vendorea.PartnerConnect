# SPR Inbound Simulation (testing the EDI feedback loop without live SPR)

Lets you drive the inbound side of the SPR flow — POACK / ASN / invoice — without a live SPR SFTP connection, and observe how it updates PartnerConnect order status and the Merchant360 callbacks. **Off by default**; safe to leave the code deployed against live SPR.

## Kill switch (config: `SprSimulation`)

```jsonc
"SprSimulation": {
  "AllowInboundInjection": false,  // POST /api/admin/spr/inbound enabled?
  "CaptureCallbacks": false        // capture M360 callbacks instead of delivering them?
}
```

| Scenario | AllowInboundInjection | CaptureCallbacks |
|---|---|---|
| **Live SPR / production** (default) | `false` | `false` |
| Self-contained test (no Merchant360 running) | `true` | `true` |
| Test with a real/mock M360 receiver | `true` | `false` (point `Merchant360:BaseUrl` at the receiver) |

- `AllowInboundInjection=false` → the inject endpoint returns **403**.
- `CaptureCallbacks=true` → M360 order-status/shipment/invoice callbacks are **not** sent over HTTP; the payloads stay on the outbox for inspection. Set both **false** before testing against live SPR.
- `CaptureCallbacks` is read by the **BackgroundWorkers** host (which delivers the outbox); `AllowInboundInjection` is read by the **API** host.

## What's wired (production behavior, always on)

Inbound documents correlate to the order by PO number (tenant-agnostically when the dealer isn't known from the feed) and:

| Inbound doc | PC order status | Merchant360 callback |
|---|---|---|
| **POACK** (success) | `Acknowledged` | order-status `Acknowledged` |
| **POACK** (ERROR ack) | `Failed` (+ error message) | order-status `Failed` (`SPR_ERROR_ACK`) |
| **ASN** (partial) | `PartiallyShipped` (lines accumulate) | shipment callback |
| **ASN** (complete) | `Shipped` | shipment callback |
| **Invoice** | (unchanged) | invoice callback |

Status changes are forward-only and recorded in `OrderStatusHistory` (visible on PC order screens). ASN application is idempotent per manifest.

## Runbook

1. **Enable** simulation in the test environment (`AllowInboundInjection=true`, `CaptureCallbacks=true`) and restart API + BackgroundWorkers.
2. **Submit an order** via `POST /api/integrations/orders` (note its `correlationId` and `poNumber`).
3. **Inject an SPR response** (admin key, `X-API-Key`):

   ```
   POST /api/admin/spr/inbound
   { "connectionId": <sprTradingPartnerId>, "documentType": "EZPOACK", "xml": "<...>" }
   ```
   `documentType` is optional (auto-detected): `EZPOACK` | `EZASNS` | `EZINV4`.

   Example success POACK (echoes the dealer PO as `CustomerPONo`):
   ```xml
   <Order EnterpriseCode="SPR" BuyerOrganizationCode="9999999.99" CustomerPONo="PO-2026-7788" OrderNo="38000001">
     <OrderLines>
       <OrderLine PrimeLineNo="1">
         <OrderLineTranQuantity TransactionalUOM="EA" OrderedQty="5" />
         <Extn><EXTNSprOrderLineList><EXTNSprOrderLine AckStatus="A" /></EXTNSprOrderLineList></Extn>
       </OrderLine>
     </OrderLines>
     <Extn><EXTNSprOrderHeaderList><EXTNSprOrderHeader PoAckStatus="A" SprSoNum="38000001" /></EXTNSprOrderHeaderList></Extn>
   </Order>
   ```
   (For an ERROR ack set `PoAckStatus="E"` / `AckStatus="E"`. ASN/invoice samples can be built from the XSDs in `docs/SPR_XML_Documentation`.)

4. **Observe PC**: the order moves to `Acknowledged`/`Failed`/`Shipped` with new `OrderStatusHistory` rows (PC order screens).
5. **Inspect the M360 callbacks** that the order generated:
   ```
   GET /api/admin/spr/inbound/callbacks?correlationId=<order correlationId>
   → { captureMode, count, callbacks: [ { messageType, status, payload, ... } ] }
   ```
   In capture mode these show the exact payloads PC would POST to `…/merchants/{merchantId}/orders/status` (and shipment/invoice) without contacting Merchant360.

## Notes
- The inject endpoint requires an **admin** API key (same as other `api/admin/*` endpoints).
- Correlation is by PO number; if the same PO exists for multiple tenants, the most recent wins (dealer-scoped match is preferred when the tenant is known).
- The order's merchant/tenant needs a numeric `ExternalId` for the M360 callback to be enqueued (otherwise it's skipped and logged).
