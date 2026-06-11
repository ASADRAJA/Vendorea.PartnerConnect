# PC → Merchant360 Callback Contract — v1.1 (reconciled to M360's inbound DTOs)

Direct, fixed, authenticated PartnerConnect → Merchant360 lifecycle callbacks, delivered reliably
via the PC Outbox. **v1.1 aligns PC's outbound wire shape to Merchant360's actually-implemented
inbound DTOs** (`PCOrderStatusCallbackRequest`, `PCShipmentCallbackRequest`, `PCInvoiceCallbackRequest`,
`PCInventorySnapshotCallbackRequest`). Payloads are canonical; **no raw SPR/EDI XML to M360**.

## Endpoints (all `POST`)

| Callback | Route | Outbox message type |
|---|---|---|
| Order status | `/api/v1/partner-connect/merchants/{merchantId}/orders/status` | `Merchant360OrderStatus` |
| Shipment | `…/merchants/{merchantId}/shipments` | `Merchant360Shipment` |
| Invoice / credit | `…/merchants/{merchantId}/invoices` | `Merchant360Invoice` |
| Inventory snapshot applied | `…/merchants/{merchantId}/inventory/snapshot` | `Merchant360InventorySnapshot` |

### `{merchantId}` — the M360 merchant id (not PC's tenant id)
M360 correlates each callback with `PCOrderIntegration.TenantId == {merchantId}`, where that is
**M360's own merchant/tenant id**. PC stores it as `Tenant.ExternalId` (synced from M360), so PC
sends `int.Parse(order.Tenant.ExternalId)` — **not** its internal `order.TenantId`. If a tenant has
no numeric `ExternalId`, the callback is skipped + logged (it can't be M360-correlated).

## Auth
M360's callback endpoints are `[AllowAnonymous]` and validate a single **`X-Api-Key`** header equal
to M360's `PartnerConnect:InboundApiKey`. PC sends `X-Api-Key` (from `Merchant360:ApiKey`) on every
call (its OAuth2 Bearer is ignored by these endpoints). **The shared key must match per environment.**

## Idempotency / event identity
Every callback carries a unique **`eventId`** (a per-delivery GUID, persisted in the outbox row, so
retries re-send the same id). M360 dedups order/shipment/invoice on `eventId`; inventory on
`snapshotId`. M360 returns `200` with `isDuplicate=true` for a repeat — PC treats that as delivered.

## Payloads (PC sends → M360 fields)

**Order status — `orders/status`**
`eventId`, `partnerConnectOrderId`, `correlationId`, `externalOrderId`, **`status`** (canonical
string: `Acknowledged`/`Processing`/`Shipped`/`PartiallyShipped`/`Completed`/`Failed`),
`partnerOrderNumber`, `occurredAt`, `errorCode` (e.g. `SPR_ERROR_ACK` on failure), `failureReason`.
*(status is a string, not a numeric enum.)*

**Shipment — `shipments`** (per-order envelope)
`eventId`, `partnerConnectOrderId`, `correlationId`, `partnerOrderNumber`, `isComplete`,
`shipments[]` where each is `{ shipmentId, carrier, trackingNumber, shippedAt, estimatedDelivery,
lines[{ lineNumber, vendorSku, quantityShipped }] }`. One callback per manifest (single-element
`shipments`); multiple shipments per order arrive over time.

**Invoice / credit — `invoices`**
`eventId`, `partnerConnectOrderId`, `correlationId`, `invoiceNumber`, **`documentType`**
(`Invoice`|`CreditMemo`), `invoiceDate`, `currency`, `subtotal`, `tax`, `shipping`, `total`,
`lines[{ lineNumber, vendorSku, description, quantity, unitPrice, lineTotal }]`.

**Inventory snapshot — `inventory/snapshot`** (lightweight)
`eventId`, `tradingPartnerId`, **`snapshotId`** (integer PC snapshot id), `itemCount`, `generatedAt`.
Summary only — no per-SKU data. One notification per active subscribed merchant.

## Delivery, retry, terminal failures
- Outbox-backed, at-least-once, exponential backoff (`30s → 8m`, `MaxRetries=5`), persisted status +
  `LastError`.
- **Transient failures** (network, 5xx, 408, 429) → retried with backoff.
- **Permanent failures** (HTTP **4xx** other than 408/429 — e.g. M360 `400` for an unknown status or
  failed correlation) → marked **Failed terminally, no retry churn**, available for manual replay.
- Source-side enqueue failures are caught/logged and never corrupt core document/snapshot processing.

## Ops: manual retry / replay
- `GET  /api/admin/outbox/stats`, `GET /api/admin/outbox/failed?skip=&take=`
- `POST /api/admin/outbox/{id}/retry`, `POST /api/admin/outbox/retry-failed?max=`

## Status responses (M360 → PC)
`200` `PCCallbackResponse{ success, message, isDuplicate, integrationId, supplierStatus }` on
success/duplicate; `400` on validation failure (unknown status, no correlation); `401` on bad key.

## Remaining items
- Confirm the shared `X-Api-Key` value matches in both environments before go-live.
- `isComplete` is sent as `null` (M360 treats null as Shipped); a true partial/complete signal would
  require richer shipment-completion tracking on the PC side (future).
- M360's `PCShipmentLineDto` is summary-level (no lot/serial/expiry); PC omits those fields.
