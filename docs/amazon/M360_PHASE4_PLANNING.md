# Merchant360 Phase 4 — implementation planning

**Date:** 2026-11-06 · For the Merchant360 team · Contract: `partnerconnect-m360-events-v1.schema.json` v1.1

**M360 can start now, against mock events.** Nothing here waits on Amazon access or on
PartnerConnect finishing Phase 3 — the contract is fixed, and mock payloads are in
`AMAZON_CONTRACT_EXAMPLES.md`.

This is the one phase where **M360 sits on the critical path**: PC can complete Phases 1–3
and still deliver nothing a merchant can use.

---

## 1. Components to build

| # | Component | Purpose | Cx |
|---|---|---|---|
| M1 | **Event receiver** `POST /api/v1/integrations/partnerconnect/events` | Single endpoint, dispatches on `eventType` | M |
| M2 | **Signature verification** | HMAC over `timestamp + "." + raw body`, ±5 min, constant-time | M |
| M3 | **Durable inbox** | Persist before processing; dedupe on `eventId` | M |
| M4 | **Async processor** | Drains the inbox; never runs inside the HTTP request | M |
| M5 | **Sales-order mapping** | Create or update the operational order | L |
| M6 | **SKU mapping store + admin UI** | `(connection, marketplace, sellerSku)` → catalogue item | L |
| M7 | **Processing-result callback** | Tell PC whether the event became an order | M |
| M8 | **Unmatched-product queue** | Operator resolution for unmapped lines | M |
| M9 | **Connection UI** | Start/stop authorisation via PC; M360 never holds Amazon secrets | M |

## 2. Inbox state machine

```
Received ──► Processing ──► Processed          ──► callback PC: Processed
                   │
                   ├──────► FailedRetryable    ──► callback PC: FailedRetryable, retry locally
                   └──────► FailedPermanent    ──► callback PC: FailedPermanent, operator queue
```

`202` is returned at **Received**, before any business work. It means stored, not processed —
the processing result is a separate, later message.

## 3. Idempotency — four distinct keys

Conflating any two of these produces duplicate orders, which is the failure the whole phase
exists to prevent.

| Key | Scope | Rule |
|---|---|---|
| `eventId` | Inbox row | Already present → return 202 immediately, do nothing else |
| `(provider, integrationConnectionId, externalOrderId)` | **Order identity** | Unique → exactly one M360 sales order, for life |
| `sourceLastUpdatedAt` + `sourceVersion` | Update ordering | Older than stored → discard, do not apply |
| `deduplicationKey` | State transition | Equal across transport retries of one transition |

**`eventId` is not order identity.** A redelivery carries a new `eventId` and the same
order identity, and must update the same order. Keying the order off `eventId` would create
a second order on every replay.

## 4. Mapping rules

| Situation | Behaviour |
|---|---|
| `mappingStatus: Mapped` with a hint | Use the hint if it resolves; otherwise treat as unmapped |
| `mappingStatus: UnmappedProduct` | **Create the order and the line**, flag for resolution, **no inventory effect** |
| `mappingStatus: Conflict` | Create, flag, no inventory effect |
| `fulfillmentChannel: AmazonFulfilled` | Record only. **Never** enters pick/pack/ship |
| `buyerDataAvailable: false` | Normal for `PENDING`. Not an error, no alert |
| Unknown `status` | Surface as unknown; never coerce to a progress state |

Never silently drop a line, invent a catalogue item, or bind by ASIN alone. An unmapped
line must be visible — a dropped one is invisible until a customer complains.

## 5. Build order against mocks

1. **M2 first.** Verify the shared test vector `a050819d…eedd0c` before anything else — a signature mismatch found later looks like a delivery failure and wastes a day.
2. **M1 + M3.** Receiver and inbox. Accept, store, return 202. No business logic yet.
3. **M4 + M5.** Process a mapped FBM order into a sales order.
4. **M7.** Close the loop back to PC.
5. **M6 + M8.** SKU mapping and the unmatched queue.
6. **M9.** Connection UI.

Steps 1–4 need only the mock payloads. By the time PC finishes Phase 3, M360 should already
be consuming them.

## 6. Tests M360 owns

| Scenario | Must prove |
|---|---|
| Duplicate `eventId` | One order, 202 both times |
| Redelivery, new `eventId`, same identity | Same order updated, **not** a second |
| Out-of-order update | Older snapshot discarded |
| Bad signature | 401, nothing stored, no retry benefit |
| Tampered timestamp | 401 — the defect the v1.1 scheme fixes |
| Handler fails after 202 | `FailedRetryable`, result sent, no duplicate |
| Unmapped SKU | Order created, flagged, **inventory untouched** |
| FBA order | Never in the fulfilment queue |
| Replay after inventory applied | No double decrement |

## 7. What M360 must not do

- Hold Amazon credentials. Authorisation lives entirely in PC.
- Call Amazon directly. Every provider interaction is PC's.
- Treat `202` as a business outcome.
- Decrement inventory for an unverified mapping.

## 8. Open items for M360

1. Which existing sales-order entity absorbs a marketplace order, and what distinguishes the channel on it.
2. Where the SKU mapping lives relative to the existing catalogue.
3. Who resolves the unmatched queue operationally.
4. Whether `FailedPermanent` raises an alert or only a queue entry.
