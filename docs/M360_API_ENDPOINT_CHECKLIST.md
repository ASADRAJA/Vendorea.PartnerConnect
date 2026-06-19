# Merchant360 → PartnerConnect: API Endpoint Checklist & Contracts

**Purpose:** the complete M360-facing API surface PartnerConnect exposes, with request/response parameters so an M360 developer can confirm or update their integration. Internal admin-portal APIs (`api/admin/*`) and PC-staff-only controllers are excluded.

---

## Authentication (ENFORCED)

- **Header:** every request must send `X-API-Key: <org key>`. Merchant360's key is its organization API key (the same key PartnerConnect uses for outbound portal callbacks).
- **Enforcement:** all endpoints below require a valid key. Anonymous requests get **401**. A key lacking the endpoint's scope gets **403**. Only `/health` is anonymous.
- **Scopes:** an organization key is granted: `orders:read`, `orders:write`, `stock:read`, `freight:read`, `content:read`, `feeds:read`, `documents:read`, `connections:read`, `connections:write`, `webhooks:read`, `webhooks:write`, `usage:read`, `partners:read`. Admin-only operations (partner transport config, price-feed upload/push) require an **admin** key and are not part of the M360 surface.
- **Org scoping:** an org key may only act within its own organization. On order submission, a mismatched `organizationId`/`organizationCode` is rejected (**403**); reads/cancels of another org's orders return **404**.

Base URL (test): see the Azure test environment. All bodies/responses are JSON.

---

## A. Order submission & tracking — `api/integrations/orders`

> **Canonical order API.** The legacy `api/v1/orders` controller has been **removed** — use these endpoints only.

### ☐ POST `/api/integrations/orders` — submit an order  · scope `orders:write`
Request body (`SubmitSupplierOrderRequest`):

| Field | Type | Req | Notes |
|---|---|----|---|
| `sourcePlatform` | string | ✅ | e.g. "Merchant360" |
| `organizationId` | int | ✅* | *or* `organizationCode`; must match the authenticated org |
| `organizationCode` | string | – | e.g. "ORG-00001" (preferred external id) |
| `merchantId` | int | ✅ | M360 merchant id → resolves the tenant (Tenant.ExternalId) |
| `partnerConnectionId` | int | ✅ | which partner + account |
| `externalOrderId` | string | ✅ | M360 order id (correlation) |
| `correlationId` | string | ✅ | tracing id |
| `idempotencyKey` | string | ✅ | duplicate-submission guard |
| `submittedBy` | string | – | username/service account |
| `poNumber` | string | ✅ | → `Order/@CustomerPONo` |
| `orderDate` | datetime | – | defaults to now |
| `notes` | string | – | order-level note |
| `externalReferences` | string(JSON) | – | extra identifiers |
| `orderType` | string | – | `StockOrder`\|`WrapAndLabel`\|`DropShip`; **default `WrapAndLabel`** (SPR 03) |
| `distributionCenterCode` | string | – | ship-from SPR DC → `Order/@ShipNode` |
| `attn` | string | – | label ATTN → `DealerAttn` (≤25) |
| `labelComments` | string[] | – | up to 3 label lines → `LabelCmmnts1..3` (≤25 each) |
| `shipTo` | object | ✅ | end customer (see Address) |
| `shipFrom` | object | – | merchant business for the label (see Address) |
| `billTo` | object | – | defaults to ship-to |
| `lines[]` | array | ✅ | order lines (see Line) |
| `allowPartialShipment` / `allowBackorder` / `allowSubstitutions` | bool | – | stored; not yet transmitted |
| `shippingPriority` / `requestedShipDate` / `requestedDeliveryDate` / `shippingMethod` | – | – | shipping preferences |

**Address** (`shipTo`/`shipFrom`/`billTo`): `name`, `company`, `address1`, `address2`, `address3`, `city`, `state`, `postalCode`, `country` (default "US"), `phone`, `email`, `isResidential` (bool → inverted to `IsCommercialAddress`).

**Line** (`lines[]`): `lineNumber` (int, optional), `vendorSku` (✅), `buyerSku`, `upc`, `quantity` (decimal ✅), `unitOfMeasure` (default "EA"), `unitPrice`, `description`, `requestedDeliveryDate`, `notes` (→ line-level SPR Note), `externalLineReference`.

Responses: **202** accepted, **200** duplicate (`isDuplicate:true`), **400** `ValidationFailed` (with `errors[]`), **403** org mismatch, **409** idempotency conflict. Body = `SubmitSupplierOrderResponse`: `accepted`, `partnerConnectOrderId`, `partnerDocumentId`, `status`, `correlationId`, `acceptedAt`, `warnings[]`, `errors[]` (`{code,field,message}`), `isDuplicate`.

### ☐ GET `/api/integrations/orders?sourcePlatform=&externalOrderId=` — look up by M360 order id  · scope `orders:read`
Returns **200** `OrderDetail` (below) or **404**.

### ☐ GET `/api/integrations/orders/{id}` — get by PartnerConnect order id  · scope `orders:read`
Returns **200** `OrderDetail` or **404** (incl. when the order belongs to another org).

### ☐ GET `/api/integrations/orders/list?status=&page=&pageSize=` — list the org's orders  · scope `orders:read`
Query: `status` (optional enum), `page` (default 1), `pageSize` (default 50, max 200). Org callers are pinned to their own org. Returns **200** array of `OrderDetail`.

### ☐ POST `/api/integrations/orders/{id}/cancel` — cancel an order  · scope `orders:write`
Body: `{ "reason": "string?" }`. Returns **200** `OrderDetail`, **400**, or **404**.

**`OrderDetail`** (`IntegrationOrderDetailDto`): `partnerConnectOrderId`, `externalOrderId`, `sourcePlatform`, `correlationId`, `status`, `poNumber`, `orderType`, `distributionCenterCode`, `attn`, `tradingPartnerId`, `partnerOrderNumber`, `orderDate`, `totalAmount`, `currency`, `lineCount`, `submittedAt`, `acknowledgedAt`, `shippedAt`, `completedAt`, `cancelledAt`, `errorMessage`, `lines[]` (`lineNumber`, `sku`, `vendorSku`, `description`, `quantity`, `unitOfMeasure`, `unitPrice`, `status`).

---

## B. Connection & partner management — `api/v1/org`

### ☐ GET `/api/v1/org/partners` — partners this org may connect to  · scope `partners:read`
Returns array of `{ tradingPartnerId, code, name, description, requiredFields[] }`.

### ☐ POST `/api/v1/org/connections` — request a tenant↔partner connection  · scope `connections:write`
Body (`OrgConnectionRequest`): `tradingPartnerId` (int ✅), `externalTenantId` (string ✅), `accountNumber` (string ✅), `contactFirstName`, `contactLastName`, `specialIdentifyingCode`, `notes`, `confirmationFields` (`{string:string}`). Returns the connection (`OrgConnectionDto`) or **400**.

### ☐ GET `/api/v1/org/connections` — list this org's connections  · scope `connections:read`
Returns array of `OrgConnectionDto`: `id`, `tradingPartnerId`, `partnerName`, `externalTenantId`, `accountNumber`, `approvalStatus`, `isActive`, `createdAt`, `decidedAt`.

### ☐ POST `/api/v1/trading-partner-subscriptions/cancel` — cancel a pending request  · scope `connections:write`
### ☐ POST `/api/v1/trading-partner-subscriptions/unsubscribe` — unsubscribe from an approved connection  · scope `connections:write`
Body for both: `{ "tenantId": <int>, "tradingPartnerId": <int> }`.
- `tenantId` = the merchant's tenant id (matched to the connection's `externalTenantId`).
- ⚠️ `tradingPartnerId` = **PartnerConnect's** trading-partner id (the `tradingPartnerId` from `GET /api/v1/org/partners`) — **not** M360's local partner id. Same convention as the connection-request call.

Behavior: **cancel** requires the connection to be `Pending` → sets it `Cancelled` (inactive); **unsubscribe** requires `Approved` → sets it `Unsubscribed` (inactive), which immediately gates off orders and live web services. Both are idempotent (already cancelled/unsubscribed → 200).
Responses: **200** `{ success, connectionId, status, isActive }`; **400** missing fields / not an org key (403); **404** no matching connection; **409** wrong state (e.g. cancel on an approved connection, or unsubscribe on a pending one).

---

## C. Live SPR web services — `api/v1/org` (gated to an active SPR connection)

### ☐ POST `/api/v1/org/stock-check` — live stock + dealer price  · scope `stock:read`
Request (`StockCheckRequest`): `externalTenantId` (✅ dealer id), `itemNumber` (✅ SPR Mfr Id + Stock Number), `dcNumbers` (int[] 1–8, optional; omit = all DCs), `availableOnly` (bool, default true).
Response (`StockCheckResponse`): `success`, `message`, `itemNumber`, `upc`, `description`, `itemStatus`, `unitOfMeasure`, `orderMinimum`, `retailPrice`, `hazmatMessage`, `pricingIncluded`, `dealerPrice`, `discountable`, `priceDescription`, `distributionCenters[]` (`dcNumber`, `dcName`, `available`, `unitOfMeasure`, `onOrder`, `expected`, `sprinter`, `cutOff`, `leadTime`, `dcType`). Failure: **400** / **403** (no active SPR connection) / **503** (not configured).

### ☐ POST `/api/v1/org/freight/rates` — all qualifying freight rates  · scope `freight:read`
### ☐ POST `/api/v1/org/freight/lowest-rate` — cheapest qualifying rate  · scope `freight:read`
Request (`FreightRateRequest`): `externalTenantId` (✅), `shipFromDc` (int ✅), `destinationState` (✅), `destinationZip` (✅), `totalWeight` (decimal lbs ✅), `carrier` (optional, SPR Table 5), `serviceLevel` (SPR Table 4 `00`–`09`; required for lowest-rate), `residential` (bool).
Response (`FreightRateResponse`): `success`, `message`, `rates[]` (`shipFromDc`, `carrier`, `carrierDescription`, `shipVia`, `rate`, `deliveryDays`, `numberOfCartons`, `serviceLevel`, `residential`). Lowest-rate returns ≤1 option. Failure: **400** / **403** / **503**.

---

## D. Catalog / content  · scope `content:read`

| ✅ | Method | Path | Purpose / params |
|----|--------|------|------------------|
| ☐ | GET | `/api/v1/dealers/{dealerId}/spr/products` | List products. Query: `search`, `locale` (default EN_US), `categoryId`, `brand`, `page`, `pageSize` |
| ☐ | GET | `/api/v1/dealers/{dealerId}/spr/products/{productId}` | Product detail |
| ☐ | GET | `/api/v1/dealers/{dealerId}/spr/products/by-sku/{sku}` | Product by SKU |
| ☐ | GET | `…/products/{productId}/accessories` · `/similar` · `/upsells` | Related products |
| ☐ | GET | `/api/v1/dealers/{dealerId}/spr/products/brands` · `/stats` | Brand list / catalog stats |
| ☐ | GET | `/api/v1/spr/categories` (+ `/{id}`, `/{id}/descendants`, `/{id}/ancestors`, `/search?term=`, `/counts`) | SPR category tree |

---

## E. Feeds (price / inventory / content)  · scope `feeds:read`

| ✅ | Method | Path | Purpose |
|----|--------|------|---------|
| ☐ | GET | `/api/v1/feeds/price/dealer/{dealerId}` (+ `/statistics`, `/{id}`) | Dealer price feed |
| ☐ | GET | `/api/v1/feeds/inventory/dealer/{dealerId}` (+ `/statistics`, `/{id}`) | Dealer inventory feed |
| ☐ | GET | `/api/v1/feeds/content/dealer/{dealerId}` (+ `/{id}`) | Dealer content feed |
| ☐ | GET | `/api/v1/pricefeeds/prices` · `/api/v1/pricefeeds/prices/search` | Query dealer prices |

*(`pricefeeds/upload` and `/push-to-merchant360` are admin-only.)*

---

## F. Documents (EDI visibility)

| ✅ | Method | Path | Scope | Purpose |
|----|--------|------|-------|---------|
| ☐ | GET | `/api/v1/documents` | `documents:read` | List documents (filtered) |
| ☐ | GET | `/api/v1/documents/{id}` · `/{id}/status` | `documents:read` | Document detail / status |
| ☐ | GET | `/api/v1/documents/stats` | `documents:read` | Document stats |
| ☐ | POST | `/api/v1/documents/{id}/reprocess` | `documents:write` | Reprocess a document |

---

## G. Webhooks / event subscriptions

| ✅ | Method | Path | Scope | Purpose |
|----|--------|------|-------|---------|
| ☐ | GET | `/api/v1/webhooks/subscriptions` · `/{id}` · `/{id}/deliveries` · `/event-types` | `webhooks:read` | List / inspect |
| ☐ | POST | `/api/v1/webhooks/subscriptions` | `webhooks:write` | Create subscription |
| ☐ | PUT / DELETE | `/api/v1/webhooks/subscriptions/{id}` | `webhooks:write` | Update / delete |
| ☐ | POST | `…/{id}/enable` · `/disable` · `/test` · `/regenerate-secret` | `webhooks:write` | Lifecycle |

Create body (`CreateWebhookRequest`) and event types are returned by `GET /event-types`. PC→M360 **order status callbacks** are a separate contract (`M360_CALLBACK_CONTRACT.md`) — endpoints M360 exposes, not PC.

---

## H. Usage  · scope `usage:read`

| ✅ | Method | Path | Purpose |
|----|--------|------|---------|
| ☐ | GET | `/api/v1/usage/current` · `/history` · `/report` · `/billing` · `/metrics/{metricType}` | Consumption & billing metrics |

---

## Notes for review
- **Org self-service onboarding** (`api/onboarding`) is currently behind the admin default-deny. If prospective orgs must register before having a key, the `request`/`verify` endpoints need `[AllowAnonymous]`.
- **AdminPortal** authenticates with an admin key (the dev-admin key in Development); in production it requires a real admin API key, since the dev-admin bypass is Development-only.
- **Per-dealer ownership** on content/feed/usage reads (verifying the org owns `{dealerId}`) is enforced at the connection level but not yet per-request on every read — a recommended follow-up.
