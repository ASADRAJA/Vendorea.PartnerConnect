# SP-API Verification — November 2026 (backlog item 0.3)

Verified against Amazon's current published documentation before writing the Orders client,
as the contract correction brief requires. **Four findings change the design**; they are marked.

Sources: `developer-docs.amazon/sp-api/reference/searchorders`,
`…/docs/orders-api-rate-limits`, `…/docs/orders-api-migration-guide`,
`…/docs/sp-api-endpoints`, `…/docs/connecting-to-the-selling-partner-api`,
and the October 2023 release announcement.

---

## 1. Authentication — simpler than assumed

**No AWS SigV4 and no IAM.** Since 2 October 2023 SP-API authenticates with a Login with Amazon
access token alone, sent in the **`x-amz-access-token`** header. Requests that still carry a
SigV4 signature have it disregarded.

This removes an entire component the architecture had allowed for. There is no AWS credential to
store, rotate or protect — which also means one fewer secret in the custody plan.

## 2. Endpoints

| Region | Host |
|---|---|
| North America (CA, US, MX, BR) | `https://sellingpartnerapi-na.amazon.com` |
| Europe (EU, UK, IN, MENA, ZA) | `https://sellingpartnerapi-eu.amazon.com` |
| Far East (JP, AU, SG) | `https://sellingpartnerapi-fe.amazon.com` |

`IntegrationConnection.Region` selects one. A connection's region is fixed at authorization and
is not a per-call parameter.

## 3. Rate limits — **finding: far tighter than the design assumed**

| Operation | Version | Rate (req/s) | Burst |
|---|---|---|---|
| `searchOrders` | 2026-01-01 | **0.0056** | 20 |
| `getOrder` | 2026-01-01 | 0.5 | 30 |
| `confirmShipment` | v0 | 2 | 10 |
| `getOrders` (old) | v0 | 0.0167 | 20 |

**0.0056 req/s is one request every ~179 seconds.** Not per minute — per three minutes, sustained,
after a burst of 20 is spent.

The consequences are structural, not tuning details:

- **Backfill is slow and must be designed as such.** A seller with 10,000 historical orders at
  100 per page needs 100 pages: 20 go immediately, the remaining 80 take **about four hours**.
  A first connection is not "synced in a few minutes", and the pilot plan should say so.
- **Steady-state polling cannot be frequent.** A five-minute poll interval per connection is
  already at the ceiling. Anything tighter simply earns 429s.
- **`getOrder` is ~90× cheaper than `searchOrders`.** Enriching a known order id is cheap;
  re-searching to discover changes is expensive. This is the argument for the SQS notification
  path (3.7) being the primary change signal and polling being the safety net — the reverse of
  how it is easy to build it.
- Limits are per application **per selling partner**, so connections do not contend with each
  other. The budget is per connection, which is what the limiter must partition on.

## 4. `searchOrders` — **finding: the parameter is `fulfillmentStatuses`, not order statuses**

`GET /orders/2026-01-01/orders`

| Parameter | Notes |
|---|---|
| `createdAfter` / `lastUpdatedAfter` | **Exactly one is required** |
| `createdBefore` / `lastUpdatedBefore` | Optional bounds |
| `fulfillmentStatuses` | `PENDING_AVAILABILITY`, `PENDING`, `UNSHIPPED`, `PARTIALLY_SHIPPED`, `SHIPPED`, `CANCELLED`, `UNFULFILLABLE` |
| `fulfilledBy` | `MERCHANT`, `AMAZON` |
| `marketplaceIds` | |
| `maxResultsPerPage` | |
| `paginationToken` | |
| `includedData` | `BUYER`, `RECIPIENT`, `PROCEEDS`, `EXPENSE`, `PROMOTION`, `CANCELLATION`, `FULFILLMENT`, `PACKAGES`, `TAX`, `PAYMENT`, `FULFILLMENT_ORDERS` |

Note `CANCELLED` with two Ls — v0 spelled it `Canceled`. A mapper carrying the v0 spelling
forward would silently classify every cancellation as `Unknown`.

**Response:** `{ orders[], pagination.nextToken, lastUpdatedBefore, createdBefore }`.

### Pagination

`nextToken` **expires after 24 hours**. Every parameter of the follow-up request must match the
first, except `maxResultsPerPage` and `includedData`.

**Finding: the response echoes the boundary.** `lastUpdatedBefore` / `createdBefore` state the
exact upper bound of the page set. That — not `max(lastUpdatedTime)` across the rows seen — is
the correct watermark. Deriving the watermark from the rows risks advancing past an order that
was modified during the walk and so never appeared on any page.

## 5. Field names — **finding: the v2026-01-01 shape is nested, not flat**

| Concept | v0 | **v2026-01-01** |
|---|---|---|
| Order id | `AmazonOrderId` | `orderId` |
| Status | `OrderStatus` | **`fulfillment.fulfillmentStatus`** |
| Purchase time | `PurchaseDate` | `createdTime` |
| Last updated | `LastUpdateDate` | `lastUpdatedTime` |
| Marketplace | `MarketplaceId` | **`salesChannel.marketplaceId`** |
| Channel | `FulfillmentChannel` (`MFN`/`AFN`) | **`fulfillment.fulfilledBy`** (`MERCHANT`/`AMAZON`) |
| Programs | scattered booleans | `programs[]` — `PRIME`, `AMAZON_BUSINESS`, `INVOICE_BY_AMAZON` |
| Money | flat `OrderTotal` | **`proceeds.grandTotal`**, needs `includedData=PROCEEDS` |
| Buyer | `BuyerInfo` | `buyer`, needs `includedData=BUYER` |
| Address | `ShippingAddress` | `recipient.deliveryAddress`, needs `includedData=RECIPIENT` |

Items: `orderItems[].orderItemId`, `.product.sellerSku`, `.product.asin`, `.product.title`,
`.quantityOrdered`, `.fulfillment.quantityFulfilled` (needs `FULFILLMENT`),
`.proceeds.breakdowns[]` (needs `PROCEEDS`).

**Status is a fulfilment status.** There is no separate order status in this version. The
canonical `SalesOrderStatus` therefore maps from `fulfillment.fulfillmentStatus`, and the
contract's `externalStatusRaw` carries that value.

**Nothing comes for free.** Money, buyer and address each require an `includedData` opt-in, and
each opt-in requires the corresponding role grant on the application. Without the PROCEEDS role
an order ingests with **no monetary values at all** — which is a working ingestion that cannot
produce a sellable Merchant360 order. That dependency belongs on the Phase 0 approval list, not
discovered in Phase 3 testing.

## 5b. Buyer and address data needs a Restricted Data Token — **gap in what is built**

Verified 2026-11-06, prompted by the Amazon permissions request being prepared.

`includedData=BUYER,RECIPIENT` is **not sufficient on its own**. Buyer identity and the shipping
address are restricted data, and reading them requires a **Restricted Data Token** obtained first:

```
POST /tokens/2021-03-01/restrictedDataToken
{
  "restrictedResources": [
    { "method": "GET", "path": "<the orders path>", "dataElements": ["buyerInfo", "shippingAddress"] }
  ]
}
→ { "restrictedDataToken": "...", "expiresIn": <seconds> }
```

Rate limit **1 req/s, burst 10**. `dataElements` also offers `buyerTaxInformation`. The RDT then
goes in `x-amz-access-token` **in place of** the ordinary LWA access token for that call.

**`AmazonOrdersClient` does not do this.** It sends the LWA access token, so orders will ingest
with no buyer and no address — recorded as `buyerDataAvailable: false`, which the model already
treats as a normal state rather than an error, so nothing breaks and nothing is silently wrong.
What it means is:

- Phase 3 ingestion is correct but PII-free until this is implemented.
- **Phase 5 merchant-fulfilled shipping cannot work at all without it** — there is no address to
  ship to.

**One value still to confirm:** the `path` inside `restrictedResources`. Amazon's reference
enumerates `dataElements` for `getOrder`, `getOrders` and `getOrderItems`, and the examples use
`/orders/v0/orders`. Whether the v2026-01-01 path is accepted there is not stated in the published
reference, and guessing it would produce a token that silently authorises nothing. It must be
confirmed against the live API once the application is registered — which is the right moment
anyway, since the RDT also depends on the restricted-data role being approved.

Tracked as backlog item **3.13**.

## 6. `confirmShipment` stays on v0

`confirmShipment` has no v2026-01-01 equivalent; it remains Orders **v0** at 2 req/s, burst 10.
Phase 5 therefore talks to two versions of the same API. That is Amazon's arrangement, not a
transitional state we can design away.

---

## What this changes

| # | Change | Affects |
|---|---|---|
| 1 | No SigV4 / IAM component | removes work; one fewer secret |
| 2 | `searchOrders` at one call per ~3 minutes | **3.4, 3.5** — poll interval floor, backfill duration, pilot expectations |
| 3 | Watermark comes from the response's echoed boundary | **3.5** |
| 4 | `CANCELLED`, nested fields, `includedData` role gating | **3.3**, and a Phase 0 approval item |
