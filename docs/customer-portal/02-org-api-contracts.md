# Customer Portal — Org API Contracts (MVP)

> Companion: [01 — IA & UX Spec](01-ia-and-ux-spec.md). Design only — no implementation yet.
> These extend the existing `OrgController` (`/api/v1/org`). DTO shapes are proposals.

## 1. Conventions

- **Base:** `/api/v1/org`
- **Auth:** `X-Api-Key` header → resolved to an **active organization** via
  `IOrgApiKeyAuthenticator` (existing). Every request is org-scoped to the key's org.
- **Tenant scoping:** the org owns multiple tenants. Tenant-specific resources are under
  `/api/v1/org/tenants/{tenantId}/…`. The server **association-gates** `tenantId` to the
  authenticated org (404 if it isn't the org's tenant) — same pattern as the existing
  DC endpoint. `tenantId` is PC's internal tenant id.
- **Pagination:** list endpoints accept `?skip=&take=` (default `take=50`, max `200`) and
  return `{ items, total, skip, take }`.
- **Filtering/sorting:** `?sort=field:asc|desc` and endpoint-specific filters (documented per endpoint).
- **Errors:** RFC 7807-style `{ type, title, status, detail, traceId }`. Never leak internals.
  - `401` missing/invalid API key · `403` org inactive / insufficient role · `404` unknown or
    non-owned resource · `409` conflict (e.g. duplicate connection request) · `422` validation.
- **Timestamps:** ISO-8601 UTC.

## 2. Already implemented (reuse)

| Method | Path | Notes |
|---|---|---|
| POST | `/org/stock-check` | SPR stock/price lookup |
| POST | `/org/freight/rates`, `/org/freight/lowest-rate` | Freight |
| GET | `/org/partners` | Partner directory |
| GET | `/org/partners/{partnerCode}/distribution-centers` | DC list (association-gated) |
| POST | `/org/connections` | Request a connection |
| GET | `/org/connections` | List the org's connections |

## 3. New endpoints (MVP)

### 3.1 Identity / bootstrap

| Method | Path | Purpose |
|---|---|---|
| GET | `/org/me` | Current org + user context: org profile, **tenants the caller can access**, role, capabilities. Drives the tenant switcher + nav gating. |
| PUT | `/org/me` | Update the current user's profile / notification prefs. |

`GET /org/me` → `OrgContextDto`:
```jsonc
{
  "organization": { "id": 12, "name": "Acme Group", "status": "Active" },
  "user": { "id": 44, "displayName": "A. Raja", "email": "…", "role": "OrgAdmin" },
  "tenants": [ { "id": 1, "name": "Alpha Dealer", "externalId": "1", "status": "Active" } ],
  "capabilities": ["connections.write", "orders.read", "users.manage"]
}
```

### 3.2 Connections (detail + config)

| Method | Path | Purpose |
|---|---|---|
| GET | `/org/connections/{code}?tenantId=` | Connection detail (status + config + capabilities). |
| PUT | `/org/connections/{code}?tenantId=` | Update configuration (account #, transport, DC selection, prefs). |
| POST | `/org/connections/{code}/suspend?tenantId=` | Suspend. |
| DELETE | `/org/connections/{code}?tenantId=` | Disconnect. |

`OrgConnectionDetailDto`:
```jsonc
{
  "partnerCode": "SPR",
  "partnerName": "S.P. Richards",
  "tenantId": 1,
  "status": "Active",                 // Pending | Active | Suspended
  "requestedAt": "…", "approvedAt": "…",
  "capabilities": ["prices","content","inventory","orders"],
  "config": {
    "partnerAccountNumber": "003382200",
    "transport": { "type": "Ftp", "host": "…", "username": "…", "hasPassword": true },
    "selectedDistributionCenters": ["0009","0025"],
    "preferences": { "orderType": "03", "contentSubscribed": true }
  },
  "health": { "lastSyncAt": "…", "lastError": null }
}
```
Notes: **never return secrets** — expose `hasPassword: true` and accept write-only `password`
on `PUT`. `PUT` validates DC codes against the partner's DC list.

### 3.3 Catalog — Prices

| Method | Path | Purpose |
|---|---|---|
| GET | `/org/tenants/{tenantId}/prices?partnerCode=&search=&skip=&take=` | Current prices (paged, searchable by SKU/description). |
| GET | `/org/tenants/{tenantId}/prices/{sku}/history?partnerCode=` | Price history for a SKU. |

`PriceRowDto`:
```jsonc
{ "sku": "C7700YS", "description": "…", "cost": 12.34, "listPrice": 19.99,
  "uom": "EA", "lastPushedAt": "…", "effectiveDate": "…" }
```

### 3.4 Catalog — Inventory

| Method | Path | Purpose |
|---|---|---|
| GET | `/org/tenants/{tenantId}/inventory?partnerCode=&search=&skip=&take=` | Stock by DC + freshness. |

`InventoryRowDto`:
```jsonc
{ "sku": "C7700YS", "byDistributionCenter": [ {"dc":"0009","onHand":120,"status":"InStock"} ],
  "asOf": "…" }
```
(Live SKU lookup reuses `POST /org/stock-check`.)

### 3.5 Catalog — Content

| Method | Path | Purpose |
|---|---|---|
| GET | `/org/tenants/{tenantId}/content/summary?partnerCode=` | Coverage summary (total SKUs, with-content, %). |
| GET | `/org/tenants/{tenantId}/content?partnerCode=&search=&skip=&take=` | Per-SKU content availability + subscription state. |

`ContentSummaryDto`: `{ "totalSkus": 84771, "withContent": 84771, "coveragePct": 100, "lastSyncAt": "…" }`

### 3.6 Orders (tracking)

| Method | Path | Purpose |
|---|---|---|
| GET | `/org/tenants/{tenantId}/orders?partnerCode=&status=&from=&to=&skip=&take=` | Orders list. |
| GET | `/org/tenants/{tenantId}/orders/{id}` | Order detail incl. the document chain. |

`OrderSummaryDto`:
```jsonc
{ "id": 501, "poNumber": "PO-1001", "partnerCode": "SPR", "orderedAt": "…",
  "status": "Acknowledged", "chain": ["PO","ACK"], "total": 842.50 }
```
`OrderDetailDto` adds `lines[]`, `documents[]` (`{type: PO|ACK|ASN|Invoice, receivedAt, viewUrl}`),
and `exceptions[]` (plain-language reasons from quarantined docs).

### 3.7 Activity & errors

| Method | Path | Purpose |
|---|---|---|
| GET | `/org/tenants/{tenantId}/activity?type=&level=&from=&to=&skip=&take=` | Unified event feed (feeds, orders, connections). |

`ActivityEventDto`:
```jsonc
{ "at": "…", "type": "PriceFeed", "level": "Info",   // Info | Warning | Error
  "title": "Price feed pushed", "detail": "46,646 records", "correlationId": "…",
  "link": "/t/1/catalog/prices" }
```

### 3.8 Dashboard summary (optional convenience)

| Method | Path | Purpose |
|---|---|---|
| GET | `/org/tenants/{tenantId}/summary` | One call for the dashboard: connection health, last sync per feed type, recent orders, open-error count. Avoids N calls on landing. |

### 3.9 Organization admin (org-admin role)

| Method | Path | Purpose |
|---|---|---|
| GET | `/org/tenants` | Tenants under the org. |
| POST | `/org/tenants` | Add/register a tenant (maps to M360 merchant where applicable). |
| GET | `/org/users` | Users in the org. |
| POST | `/org/users/invite` | Invite a user (email, role, tenant scope). |
| PUT | `/org/users/{id}` | Update role / tenant scope / status. |
| DELETE | `/org/users/{id}` | Deactivate. |
| GET/PUT | `/org/settings` | Org profile + notification preferences. |

## 4. Cross-cutting

- **Association-gating** on every `tenantId` and `{code}` — a request may only touch the
  authenticated org's tenants/connections. Return `404` (not `403`) for non-owned ids to
  avoid enumeration.
- **RBAC** enforced server-side per capability (don't rely on the UI hiding actions):
  `connections.write`, `orders.read`, `users.manage`, etc. — surfaced in `GET /org/me`.
- **Secrets:** never returned; write-only on input; represented by `hasX` booleans on read.
- **Versioning:** these live under `v1` alongside the existing org endpoints.
- **Rate limiting / usage:** reads count toward the org's metered usage (existing metering).

## 5. Endpoint → screen map (traceability)

| Screen | Endpoint(s) |
|---|---|
| Dashboard | `GET /org/tenants/{id}/summary` |
| Connections list | `GET /org/connections` |
| Request connection | `GET /org/partners`, `POST /org/connections` |
| Connection detail | `GET/PUT /org/connections/{code}`, `GET /org/partners/{code}/distribution-centers` |
| Prices | `GET /org/tenants/{id}/prices`, `.../prices/{sku}/history` |
| Inventory | `GET /org/tenants/{id}/inventory`, `POST /org/stock-check` |
| Content | `GET /org/tenants/{id}/content[/summary]` |
| Orders | `GET /org/tenants/{id}/orders`, `.../orders/{id}` |
| Activity | `GET /org/tenants/{id}/activity` |
| Organization | `GET/POST /org/tenants`, `/org/users*`, `/org/settings` |
| Account | `GET/PUT /org/me` |

## 6. Not in MVP (later)

`POST` submit orders, content subscription writes, webhook subscriptions + API keys,
billing/invoices. These get their own `v1` endpoints in Phase 2/3.
