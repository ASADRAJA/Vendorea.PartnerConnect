# Amazon contract — example messages and sequences

Companion to the OpenAPI and JSON Schema files. Every example validates against
`partnerconnect-m360-events-v1.schema.json`. PII is least-necessary throughout.

---

## 1. Standard FBM order, created

```json
{
  "eventId": "7f1c0b2a-9d3e-4a51-8c77-2b9e4a1f6d30",
  "eventType": "sales_order.created.v1",
  "eventVersion": "v1",
  "occurredAt": "2026-11-05T14:02:11Z",
  "publishedAt": "2026-11-05T14:02:13Z",
  "organizationId": 2,
  "tenantId": 1,
  "integrationConnectionId": "c0a81f22-5e44-4d0b-9f31-7a2b6c8d1e90",
  "provider": "AMAZON_SP",
  "marketplaceId": "ATVPDKIKX0DER",
  "correlationId": "sync-2026-11-05T14:02:00Z-page1",
  "deduplicationKey": "AMAZON_SP:c0a81f22:111-2233445-6677889:2026-11-05T14:02:11Z",
  "payloadKind": "snapshot",
  "payload": {
    "identity": {
      "provider": "AMAZON_SP",
      "integrationConnectionId": "c0a81f22-5e44-4d0b-9f31-7a2b6c8d1e90",
      "externalOrderId": "111-2233445-6677889"
    },
    "pcSalesOrderId": "9b2d7e10-33af-4c58-a0e1-5d6f8b4c2a71",
    "status": "ReadyToFulfill",
    "externalStatusRaw": "UNSHIPPED",
    "externalFulfillmentStatusRaw": null,
    "fulfillmentChannel": "MerchantFulfilled",
    "mappingStatus": "Mapped",
    "programsRaw": ["PRIME"],
    "purchaseDate": "2026-11-05T13:58:02Z",
    "sourceLastUpdatedAt": "2026-11-05T14:02:11Z",
    "sourceVersion": "3",
    "orderTotal": { "amount": "48.97", "currency": "USD" },
    "buyerDataAvailable": true,
    "buyer": { "emailRef": "x1y2z3@marketplace.amazon.com", "name": "J. Carter" },
    "shipTo": {
      "name": "J. Carter", "line1": "414 W Main St", "line2": null,
      "city": "Richmond", "stateOrRegion": "VA", "postalCode": "23220", "countryCode": "US"
    },
    "lines": [
      {
        "externalLineId": "58392014771234",
        "sellerSku": "VND-COPY-8511",
        "asin": "B07XYZ1234",
        "title": "Copy paper, 8.5x11, 500 sheets",
        "quantityOrdered": 2,
        "quantityShipped": 0,
        "mappingStatus": "Mapped",
        "m360CatalogItemIdHint": "ITEM-44821",
        "itemPrice": { "amount": "39.98", "currency": "USD" },
        "itemTax": { "amount": "2.40", "currency": "USD" },
        "shippingPrice": { "amount": "6.59", "currency": "USD" },
        "promotionDiscount": { "amount": "0.00", "currency": "USD" },
        "proceedsRaw": { "type": "Principal", "charges": [{ "type": "Principal", "amount": "39.98" }] }
      }
    ]
  }
}
```

`programsRaw: ["PRIME"]` — the consumer derives "this is Prime". No `isPrime` boolean crosses the wire.

## 2. PENDING order — buyer data legitimately absent

Note `buyerDataAvailable: false`, `buyer: null`, `shipTo: null`. **This is a normal provider
state, not an integration failure, and must never be alerted as one.**

```json
{
  "eventId": "2d9a7c41-6b0f-4e22-9a18-ef3c5d7a9b02",
  "eventType": "sales_order.created.v1",
  "eventVersion": "v1",
  "occurredAt": "2026-11-05T15:10:00Z",
  "publishedAt": "2026-11-05T15:10:01Z",
  "organizationId": 2,
  "tenantId": 1,
  "integrationConnectionId": "c0a81f22-5e44-4d0b-9f31-7a2b6c8d1e90",
  "provider": "AMAZON_SP",
  "marketplaceId": "ATVPDKIKX0DER",
  "correlationId": "notif-ORDER_CHANGE-8891",
  "deduplicationKey": "AMAZON_SP:c0a81f22:111-9988776-5544332:2026-11-05T15:10:00Z",
  "payload": {
    "identity": {
      "provider": "AMAZON_SP",
      "integrationConnectionId": "c0a81f22-5e44-4d0b-9f31-7a2b6c8d1e90",
      "externalOrderId": "111-9988776-5544332"
    },
    "pcSalesOrderId": "4e8c1a93-77bd-4f06-9c2a-1b5e9d3f7c44",
    "status": "Pending",
    "externalStatusRaw": "PENDING",
    "externalFulfillmentStatusRaw": null,
    "fulfillmentChannel": "MerchantFulfilled",
    "mappingStatus": "Mapped",
    "programsRaw": [],
    "purchaseDate": "2026-11-05T15:09:44Z",
    "sourceLastUpdatedAt": "2026-11-05T15:10:00Z",
    "orderTotal": { "amount": "19.99", "currency": "USD" },
    "buyerDataAvailable": false,
    "buyer": null,
    "shipTo": null,
    "lines": [
      {
        "externalLineId": "58392099887766",
        "sellerSku": "VND-PEN-0042",
        "asin": "B01AAA2222",
        "title": "Gel pens, black, 12 pack",
        "quantityOrdered": 1,
        "quantityShipped": 0,
        "mappingStatus": "Mapped",
        "m360CatalogItemIdHint": "ITEM-10093",
        "itemPrice": { "amount": "19.99", "currency": "USD" },
        "itemTax": { "amount": "0.00", "currency": "USD" },
        "shippingPrice": { "amount": "0.00", "currency": "USD" },
        "promotionDiscount": { "amount": "0.00", "currency": "USD" },
        "proceedsRaw": null
      }
    ]
  }
}
```

## 3. Unmapped product line

`mappingStatus: "UnmappedProduct"`, `m360CatalogItemIdHint: null`.
**M360 still creates the order**, flags the line, and applies **no inventory effect**.
Dropping the line, inventing an item, or binding by ASIN alone are all prohibited.

```json
{
  "externalLineId": "58392055443322",
  "sellerSku": "LEGACY-SKU-9",
  "asin": "B09QQQ7777",
  "title": "Desk organiser, walnut",
  "quantityOrdered": 1,
  "quantityShipped": 0,
  "mappingStatus": "UnmappedProduct",
  "m360CatalogItemIdHint": null,
  "itemPrice": { "amount": "34.50", "currency": "USD" },
  "itemTax": { "amount": "2.07", "currency": "USD" },
  "shippingPrice": { "amount": "0.00", "currency": "USD" },
  "promotionDiscount": { "amount": "0.00", "currency": "USD" },
  "proceedsRaw": null
}
```

## 4. FBA order — read-only

`fulfillmentChannel: "AmazonFulfilled"`, `status: "Fulfilled"`, `externalStatusRaw: "SHIPPED"`.

M360 records it for reporting and **must not** place it in a pick/pack/ship queue.
Amazon already shipped it; a warehouse pick would ship the goods twice.

## 5. Processing result — M360 back to PC

```json
{
  "sourceEventId": "7f1c0b2a-9d3e-4a51-8c77-2b9e4a1f6d30",
  "outcome": "Processed",
  "m360SalesOrderId": "SO-2026-004412",
  "error": null,
  "attemptedAt": "2026-11-05T14:02:19Z"
}
```

Failure variant: `"outcome": "FailedPermanent"`, `"error": "No merchant mapping for tenant 1"`.
PC records it, surfaces it on sync health, and does **not** retry delivery — the event
was accepted; the business step failed and needs an operator.

---

## 6. End-to-end sequences

### 6.1 Happy path

```
Amazon          PC                                  M360
  |   ORDER_CHANGE -> SQS                            |
  |------------------>|                              |
  |                   | record receipt, dedupe       |
  |<-- getOrder ------|                              |
  |-- order payload ->|                              |
  |                   | TX{ snapshot + outbox row }  |
  |                   | delete from SQS (after commit)|
  |                   |-- sales_order.created.v1 --->|
  |                   |                              | verify sig, inbox store
  |                   |<--------- 202 New -----------|
  |                   |                              | create SO-2026-004412
  |                   |<-- processing-result Processed|
```

The SQS delete happens **after** the commit. Deleting on receipt loses the order if
the fetch or commit then fails.

### 6.2 Duplicate — poll and notification race

Notification path stores the order. The scheduled poll returns the same order minutes later.

- Unique index on `(IntegrationConnectionId, ExternalOrderId)` rejects the insert.
- PC upserts; `sourceLastUpdatedAt` is not newer, so **no event is published**.
- M360 sees nothing. **One order, one M360 record.**

If the poll *did* carry a newer `sourceLastUpdatedAt`, an `updated.v1` is published with a
new `eventId` and the same identity — M360 updates the same order.

### 6.3 Webhook accepted, handler fails

1. M360 returns 202; event durably stored.
2. Handler throws; inbox entry -> `FailedRetryable`.
3. M360 calls processing-result with `FailedRetryable`.
4. PC surfaces it on sync health. **PC does not re-deliver** — M360 holds the event and retries internally.
5. If M360 lost it entirely, the operator calls `POST .../redeliver`: a **new eventId**, same identity, same order updated. No duplicate.

### 6.4 Handler succeeded, result callback failed

M360 created the order; the callback to PC failed. PC's last known state is
"delivery acknowledged, outcome unknown".

Resolution is **scheduled reconciliation**: PC asks for orders whose last event was
acknowledged but has no processing result beyond a threshold, and M360 replies from its
inbox. No duplicate order is created, because order identity — not event id — governs.

### 6.5 Fulfilment timeout after acceptance (C4)

```
M360 -> PC   POST .../fulfillment  (Idempotency-Key: ship-SO4412-1)
PC           persist submission { state: Requested }   <-- BEFORE any provider call
PC -> Amazon submit shipment
             ... network timeout. Accepted or not? Unknown.
PC           state -> OutcomeUnknown
```

**No automatic retry.** A retry could confirm the same shipment twice. Instead:

1. Reconciliation reads the order's fulfilment state from Amazon.
2. Already shipped -> `Confirmed`, `externalConfirmationId` recorded, M360 notified.
3. Not shipped -> `RetryableFailure`, and only then may a resubmission occur.

A replay of the same `Idempotency-Key` returns the original submission, never a second write.

### 6.6 Late, out-of-order update

Snapshot A (`sourceLastUpdatedAt` 14:02) is stored. A delayed poll yields snapshot B
(13:55). B is **discarded** — older than stored. No event. No regression of a shipped
order back to unshipped.

### 6.7 Amazon Business partial acceptance (Phase 7)

`placeOrder` returns 200 with both arrays populated:

```json
{
  "acceptedItems": [{ "lineId": "1", "quantity": 10 }],
  "rejectedItems": [{ "lineId": "2", "quantity": 5, "code": "OUT_OF_STOCK",
                      "message": "Quantity unavailable" }]
}
```

HTTP 200 is **not** acceptance. The PO moves to a partially-accepted state; rejected
lines carry their code to M360. Mapping this onto a binary accepted/failed PO status
would misreport the order.
