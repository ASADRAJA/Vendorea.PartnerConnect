# PC → Merchant360 Callback Contract — v1.0 (FROZEN)

Direct, fixed, authenticated PartnerConnect → Merchant360 lifecycle callbacks. Delivered
reliably via the PC Outbox (retry/backoff). Payloads are **canonical and merchant-scoped**;
**raw SPR/EDI XML is never sent to M360**. This is the frozen contract for the four lifecycle
callbacks; field-level conformance should be confirmed against M360's implementation in
integration testing.

> Scope note: this freezes the PC→M360 **callback** contract. The M360→PC **order intake**
> contract is separate and unchanged.

## Endpoints (all `POST`, merchant-scoped)

| Callback | Route | PC client method | Outbox message type |
|---|---|---|---|
| Order status | `…/merchants/{merchantId}/orders/status` | `PushOrderStatusUpdateAsync` | `Merchant360OrderStatus` |
| Shipment | `…/merchants/{merchantId}/shipments` | `PushShipmentUpdateAsync` | `Merchant360Shipment` |
| Invoice / credit | `…/merchants/{merchantId}/invoices` | `PushInvoiceUpdateAsync` | `Merchant360Invoice` |
| Inventory snapshot applied | `…/merchants/{merchantId}/inventory/snapshot` | `PushInventorySnapshotNotificationAsync` | `Merchant360InventorySnapshot` |

Base path prefix: `/api/v1/partner-connect`. `merchantId` == the PartnerConnect tenant id.

## Auth expectations
- **OAuth2 client-credentials** (bearer), scopes `merchant360.prices.write merchant360.content.write`
  (and order/shipment/invoice/inventory write as M360 defines), with **`X-Api-Key` header fallback**.
- Configured in the PC `Merchant360` settings section (`BaseUrl`, `TokenEndpoint`, `ClientId`,
  `ClientSecret`, `ApiKey`). Tokens cached with a refresh buffer. **HTTPS in production.**
- Both the API and BackgroundWorkers hosts use the same client/auth; callbacks enqueued by the API
  are delivered by the worker.

## Delivery, retry, idempotency
- **At-least-once** via Outbox: exponential backoff `30s → 1m → 2m → 4m → 8m`, `MaxRetries=5`,
  persisted status (`Pending/Processing/Delivered/Retry/Failed`) + `LastError`.
- A non-2xx / unsuccessful response **fails the message → retried** (not lost). Enqueue failures at
  the source are caught/logged and never corrupt core document/snapshot processing.
- **Idempotency / event identity:** every payload carries `CorrelationId` (distributed-tracing id
  for the order/document chain). M360 must **dedupe** on the natural key per callback:
  - order status → `PoNumber` + `StatusType` + `SourceDocumentId`
  - shipment → `ShipmentId` (+ `PoNumber`)
  - invoice → `InvoiceNumber` (+ `PoNumber`)
  - inventory → `SnapshotId`
- Ordering is **not** guaranteed; consumers should apply by `StatusDate` / `AppliedAt` and the
  natural key rather than arrival order.

## Payloads (canonical)

### Order status — `OrderStatusUpdateRequest`
`TradingPartnerId`, `TradingPartnerCode`, `PoNumber`, `SupplierOrderNumber`, `StatusType` (enum),
`StatusCode`, `StatusMessage`, `StatusDate`, `SourceDocumentType`, `SourceDocumentId`,
`LineUpdates[]`, **`PartnerConnectOrderId`**, **`CorrelationId`**, **`ExternalOrderId`**,
**`PreviousStatus`**.

### Shipment — `ShipmentUpdateRequest`
`TradingPartnerId`, `TradingPartnerCode`, `PoNumber`, `SupplierOrderNumber`, `ShipmentId`,
`BillOfLadingNumber`, `CarrierCode`, `CarrierName`, `TrackingNumber`, `TrackingUrl`, `ShipMethod`,
`ShipDate`, `EstimatedDeliveryDate`, `ActualDeliveryDate`, `ShipFrom`, `ShipTo`, `TotalWeight`,
`WeightUnit`, `PackageCount`, `Lines[]` (`LineNumber`, `StockNumber`, `QuantityShipped`, `LotNumber`,
`ExpirationDate`, `SerialNumber`), `Cartons[]`, **`PartnerConnectOrderId`**, **`CorrelationId`**,
**`ExternalOrderId`**. One callback per shipment manifest (multiple shipments per order over time).

### Invoice / credit — `InvoiceUpdateRequest`
`TradingPartnerId`, `TradingPartnerCode`, `PoNumber`, `SupplierOrderNumber`, `InvoiceNumber`,
`InvoiceDate`, `DueDate`, `PaymentTerms`, `SubTotal`, `TaxAmount`, `ShippingAmount`, `DiscountAmount`,
`TotalAmount`, `Currency`, **`IsCreditMemo`**, `Lines[]` (`LineNumber`, `StockNumber`, `Description`,
`Quantity`, `UnitPrice`, `ExtendedPrice`, `TaxAmount`, `DiscountAmount`), **`PartnerConnectOrderId`**,
**`CorrelationId`**, **`ExternalOrderId`**. One callback per invoice/credit in a batched file;
credit memos set `IsCreditMemo=true` (negative totals).

### Inventory snapshot applied — `SupplierInventorySnapshotNotificationRequest` (lightweight)
`TradingPartnerId`, `TradingPartnerCode`, `SnapshotId`, `SourceSnapshotId`, `CorrelationId`,
`InventoryDate`, `IsFullRefresh`, `AppliedAt`, `TotalItemCount`, `NewItemCount`, `UpdatedItemCount`,
`RemovedItemCount`, `UnchangedItemCount`. **Counts only — not the full item list.** One notification
per active subscribed merchant on the snapshot's trading partner; M360 pulls/refreshes detail.

## Canonical status values

`OrderStatusType`: `Acknowledged`, `Processing`, `PartiallyShipped`, `Shipped`, `Delivered`,
`Invoiced`, `Completed`, `Cancelled`, `Backordered`, `Failed`.

`StatusCode` (string, alongside the enum): e.g. `SPR_POACK` (success ack), `SPR_ERROR_ACK`
(order not processed / translation failure). `PreviousStatus` carries the prior order status.

## Triggers (PC side)
- POACK processed → order status (`Acknowledged` / `Failed`).
- EZASNS manifest parsed → shipment callback (per manifest).
- EZINV4 invoice/credit parsed → invoice callback (per document).
- Inventory full-refresh snapshot applied → snapshot-applied notification (per merchant).

## Ops: manual retry / replay
Dead-lettered (Failed) callbacks can be inspected and replayed via the admin outbox surface:
- `GET  /api/admin/outbox/stats` — pending/processing/retry/failed/delivered-24h counts.
- `GET  /api/admin/outbox/failed?skip=&take=` — failed messages (type, correlationId, lastError, timestamps).
- `POST /api/admin/outbox/{id}/retry` — replay one Failed/Cancelled message.
- `POST /api/admin/outbox/retry-failed?max=` — replay all currently-failed messages.

Requeue resets the retry budget and schedules immediate pickup by the worker; the prior `LastError`
is retained for audit until the next attempt.

## Open items
- Field-level conformance with M360's implemented request schemas (confirm in integration testing).
- An explicit event-id header could be added if M360 prefers header-based idempotency over the
  natural keys above.
