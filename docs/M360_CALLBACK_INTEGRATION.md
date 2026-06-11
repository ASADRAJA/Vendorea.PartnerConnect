# PC → Merchant360 Lifecycle Callbacks

Direct, fixed, authenticated PC → M360 lifecycle callbacks delivered reliably through the
existing **Outbox** (no generic webhook subscription system). Polling fallback remains available.
Payloads are canonical and merchant-scoped; **raw SPR XML is never exposed to M360**.

## Current vs final integration matrix

| Callback | Before this pass | After this pass | M360 endpoint |
|---|---|---|---|
| Order status (ack / **failed incl. SPR ERROR ack**) | Direct call, non-persisted try/catch | **Outbox-backed**, retriable, hardened DTO | `POST …/merchants/{id}/orders/status` — **confirmed** |
| Inventory (incremental) | Defined, unused, **wrong route** | **Wired** from snapshot apply, per-merchant, chunked, Outbox-backed | `POST …/merchants/{id}/inventory/batch` — **confirmed (route fixed)** |
| Shipment (EZASNS) | Defined, unused | **Deferred** — held until M360 exposes an endpoint | `…/shipments` — **NOT documented by M360** |
| Invoice (EZINV4) | Defined, unused | **Deferred** — held until M360 exposes an endpoint | `…/invoices` — **NOT documented by M360** |
| Full inventory snapshot | Defined, unused | **Not used** (incremental chosen this pass) | `…/inventory/snapshot` — not documented |

## What is now truly live
- **Order status** is enqueued via the outbox from POACK processing:
  - success POACK → `OrderStatusType.Acknowledged` / `StatusCode="SPR_POACK"`,
  - ERROR ack → `OrderStatusType.Failed` / `StatusCode="SPR_ERROR_ACK"` with the normalized message.
- **Inventory** changed-items are enqueued per subscribed merchant on snapshot apply (incremental,
  chunked at 500 items/message) to `/inventory/batch`.

## Triggers wired
- **POACK** (`SprXmlDocumentProcessingService.ApplyAckToOrderAsync` → `SurfaceAckToM360Async`):
  enqueues `Merchant360OrderStatus`.
- **Inventory apply** (`InventoryFullRefreshService.ApplySnapshotAsync` →
  `EnqueueInventoryCallbacksAsync`): fans out over active `TenantPartnerAccount`s for the snapshot's
  trading partner; enqueues `Merchant360InventoryBatch` per merchant per chunk.

## Reliability / delivery
- Routed through `OutboxMessage` + `OutboxProcessorWorker` (BackgroundWorkers host).
- **Retry with exponential backoff** (30s → 1m → 2m → 4m → 8m, `MaxRetries=5`), **last-error** and
  delivery status (`Pending/Processing/Delivered/Retry/Failed`) persisted per message.
- The outbox drain (`DefaultOutboxMessageProcessor`) calls `IMerchant360Client` and **throws on a
  non-success response**, so the message is retried rather than lost.
- **Non-fatal at the source:** enqueue failures are caught/logged and never corrupt core document or
  snapshot processing.
- **Auth:** reuses the existing Merchant360 client auth (OAuth2 client-credentials + API-key
  fallback) — both the API and BackgroundWorkers hosts register the connector, so callbacks enqueued
  in the API are delivered by the worker.
- **Manual retry/replay:** no admin/ops surface exists yet (failed messages remain in the outbox with
  `Failed` status); a small admin endpoint to re-enqueue is recommended follow-up.

## Canonical DTO additions (additive, non-breaking)
`OrderStatusUpdateRequest` gained: `PartnerConnectOrderId`, `CorrelationId`, `ExternalOrderId`,
`PreviousStatus` (newStatus is `StatusType`). The frozen M360 **intake** contract is unchanged.

## Files changed
- `Contracts/Interfaces/IMerchant360Client.cs` — `OrderStatusUpdateRequest` correlation fields.
- `Application/Services/Merchant360OutboxMessages.cs` *(new)* — message-type constants + envelopes.
- `Application/Services/OutboxService.cs` — processor handles `Merchant360OrderStatus` / `Merchant360InventoryBatch`.
- `Infrastructure/SprContent/SprXmlDocumentProcessingService.cs` — order-status push → outbox.
- `Application/Services/InventoryFullRefreshService.cs` — incremental inventory callback fan-out.
- `Merchant360Connector/Merchant360ApiClient.cs` — inventory route → `/inventory/batch`.
- Tests: `Integration/Merchant360CallbackTests.cs` *(new)*, `Integration/SprFlowSmokeTests.cs`,
  `Services/InventoryFullRefreshServiceTests.cs`, `Integration/DocumentWorkflowIntegrationTests.cs`.

## Tests
- Outbox processor dispatches order-status → client; **non-success throws → retriable**.
- Outbox processor dispatches inventory-batch → `UpdateInventoryAsync`.
- Inventory apply enqueues one incremental callback per active subscribed merchant (inactive excluded).
- SPR smoke flows updated: POACK success/ERROR ack now assert the **enqueued** callback payload.
- Build: 0 errors. Tests: **96/96 pass**.

## Remaining gaps (require M360-side work)
- **Shipment** and **invoice** callbacks are intentionally **not wired** — M360 does not document
  inbound `/shipments` or `/invoices` endpoints. Once M360 confirms/exposes them, wiring mirrors the
  inventory pattern: trigger from `ProcessAsnAsync` / `ProcessInvoiceAsync`, correlate to the merchant
  by PO number, enqueue a new outbox message type.
- **Manual retry/replay admin surface** for failed outbox messages.
- Order statuses beyond ack/failed (processing / partially-shipped / completed) await their own
  triggers (partially-shipped/shipped would derive from the deferred shipment flow).

## Assumptions
- **`merchantId == tenantId`** in the PC → M360 push contract (matches existing price/subscription usage).
- Inventory is partner-level; it fans out to **every active `TenantPartnerAccount`** on the snapshot's
  trading partner.
