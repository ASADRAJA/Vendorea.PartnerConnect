# Amazon contract — compatibility, security and migration

Companion to the OpenAPI and JSON Schema files.

---

## 1. Existing contract guarantees

**Frozen. Any change is a defect, not a design choice.**

| Contract | Guarantee | Enforced by |
|---|---|---|
| `Merchant360OutboxMessageTypes` — OrderStatus, Shipment, Invoice, InventorySnapshot | Byte-compatible payloads | Extend `Merchant360WireContractTests` to assert all four |
| `/api/integrations/orders/**` (supplier PO) | Routes, shapes, status codes unchanged | Integration regression |
| SPR EDI 850 / 855 / 856 / 810 and SFTP | Unchanged through Phase 7 | Full round-trip regression before the `Orders` migration merges |
| `Order`, `OrderLine` semantics | Remain supplier purchase orders | A marketplace sale never becomes an `Order` |

A marketplace event is **never** published on a supplier-PO message type. Separate
namespace, separate consumer path.

## 2. Authentication and authorisation

### PC APIs

| Surface | Credential | Scope |
|---|---|---|
| Integration management | Org user JWT or org API key | `integrations:read` / `integrations:write` |
| Sales order read | Org user JWT or org API key | `sales:read` |
| Fulfilment submit | Org user JWT or org API key | `sales:write` |
| Reconciliation / redeliver | Org user JWT or org API key | `sales:write` |
| OAuth callback | none (anonymous) | state-bound, rate-limited |

New scopes follow the existing convention: a `:write` suffix is dropped automatically
for Viewer by `ScopesForRole`, so no role logic changes. **Assumption A7** — verify.

Every tenant-scoped route resolves through the existing tenant-scope choke point and
returns **404, not 403**, outside scope. Organization ids supplied in a request body are
never trusted; the authenticated principal decides.

### PC → M360 event signing (corrected in v1.1)

**v1.0 defect.** The signature covered the body only. The timestamp travelled in its own
header, unsigned, so an attacker replaying a captured body could set any timestamp and the
five-minute window gave no protection. The existing `WebhookService.GenerateSignature`
has the same flaw and **cannot be reused as-is**.

**v1.1 construction.** The signed message is the timestamp and the raw body, joined by a
full stop:

```
signingString = <X-PC-Signature-Timestamp> + "." + <raw request body bytes>
signature     = lowercase_hex( HMAC_SHA256( secret, utf8(signingString) ) )
```

Headers, all three required:

| Header | Value |
|---|---|
| `X-PC-Signature-Timestamp` | RFC 3339 UTC, e.g. `2026-11-05T14:02:13Z` |
| `X-PC-Signature-KeyId` | Identifies which secret signed it; enables rotation |
| `X-PC-Signature` | `v1=<lowercase hex>` |

Verification order on the consumer, before any payload parsing:

1. All three headers present, else 401.
2. Timestamp within ±5 minutes of now, else 401. Reject, do not clamp.
3. Resolve the secret by `KeyId`. Unknown key id, else 401.
4. Recompute over `timestamp + "." + raw body` and compare in **constant time**
   (`CryptographicOperations.FixedTimeEquals`), else 401.
5. Only now deserialise.

Verify against the **raw** bytes as received. Re-serialising the parsed object changes
whitespace and key order and breaks the comparison.

**Rotation.** `KeyId` lets a new secret be introduced while the old one still verifies.
Publish with the new key, accept both for an overlap window, then retire the old id.
No redeploy and no delivery gap.

**Identical on both sides.** PartnerConnect and Merchant360 implement the same function.
A shared test vector is checked into both repositories so a divergence fails CI rather
than production.

**Test vector** (fixed secret, fixed body — both repos must reproduce it):

```
secret    = "pc-test-secret"
timestamp = "2026-11-05T14:02:13Z"
body      = {"eventId":"7f1c0b2a-9d3e-4a51-8c77-2b9e4a1f6d30"}
signingString = 2026-11-05T14:02:13Z.{"eventId":"7f1c0b2a-9d3e-4a51-8c77-2b9e4a1f6d30"}
```

Required tests: valid signature; tampered body; tampered timestamp; replay outside the
window; unknown key id; signature from the old key during overlap; body re-serialised
(must fail).

### Three distinct authentication models (C2, v1.1)

v1.0 applied webhook HMAC too broadly in the Merchant360 spec. Corrected — they are not
interchangeable, and a merchant-facing screen must never authenticate by webhook secret:

| # | Channel | Mechanism | Authorises |
|---|---|---|---|
| 1 | PC → M360 events | HMAC signature above | A trusted service notification. No user identity |
| 2 | M360 → PC APIs | Service-to-service credential (org API key or client-credentials token) + org/tenant authorisation | A named service acting for one organization |
| 3 | M360 user APIs (SKU mapping, connection admin UI) | M360's own user authentication and RBAC | A person, with roles |

Model 1 never authorises a read. Model 3 never uses a signing secret. SKU-mapping and
merchant management endpoints are **model 3**.

## 3. Idempotency

| Layer | Key | Guarantee |
|---|---|---|
| Order identity | `(provider, integrationConnectionId, externalOrderId)` | One M360 sales order, for life |
| Event inbox | `eventId` | Duplicate receive is a success, not an error |
| State transition | `deduplicationKey` | Two publications of one transition are equal |
| Update ordering | `sourceLastUpdatedAt` + `sourceVersion` | Older snapshot discarded, never applied |
| Fulfilment | caller `Idempotency-Key` + external verification | No duplicate shipment confirmation |
| PC ingest | unique index `(IntegrationConnectionId, ExternalOrderId)` | Poll/notification race collapses to one row |

**Delivery is at-least-once. Business effects are effective-once.** No exactly-once
claim is made at the network level, because none can be honoured.

## 4. Versioning

- Event types carry their version in the name (`sales_order.created.v1`). A new
  **required** field, a removed field or a changed meaning ⇒ `.v2`, published alongside
  until consumers migrate.
- Additive optional fields do not bump the version. **Consumers must ignore unknown fields.**
- API paths are `/api/v1/...`. A breaking change is `/api/v2/...`, never an in-place edit.
- `payloadKind` is `snapshot` in v1. Deltas, if ever introduced, arrive as a new kind
  and a new version — never silently.
- OpenAPI and JSON Schema validation belong in CI, with consumer-driven contract tests
  on both repositories.

## 4b. Vendorea's role and account ownership

Vendorea is a software and integration provider. Each dealer transacts through its **own**
authorized Amazon account; Vendorea is never the merchant of record, the purchasing entity, or an
intermediary receiving a dealer's proceeds.

A seller account maps to **one** Merchant360 tenant, enforced by a filtered unique index rather
than by service-layer checks. A tenant may hold several Amazon accounts. One account reachable by
more than one tenant requires an explicit, administrator-created, attributable delegation that no
OAuth callback or organization API key can produce.

Rate-limit buckets are keyed per selling partner because Amazon meters that way. They carry no
tenant id, credential, order or customer data, and a test pins their column list so that cannot
change silently.

The same applies to Amazon Business purchasing: the dealer's buying account, purchasing group and
payment arrangements, never Vendorea's.

Full statement and the tests holding it: `VENDOREA_ROLE_AND_ACCOUNT_OWNERSHIP.md`.

## 5. PII and retention

| Data | Rule |
|---|---|
| Buyer name, address | Collected only where a capability needs it; `includedData` requests the minimum |
| Buyer email | Provider-anonymised reference where supplied; never a raw personal address if avoidable |
| Raw provider payloads | Redacted and retention-bounded. **Not archived indefinitely** |
| Access tokens | Memory only, never persisted, never logged |
| Refresh tokens | Encrypted at rest, key outside the database |
| Logs and telemetry | No tokens, no buyer PII, no raw headers |
| Support views | Connection health and state; never secrets |

`PENDING` orders without buyer data are normal. Absence is recorded as
`buyerDataAvailable: false`, never as an error state.

Retention periods are an **open decision** — they depend on Amazon's data policies for
the granted roles and should be set in Phase 0, not defaulted.

## 6. Credential storage

| Secret | Store | Rotation |
|---|---|---|
| LWA client id / secret | `ISecretsManager` (Key Vault or env-backed) | Operator, per environment |
| Seller refresh token | `IntegrationCredential.ProtectedValue`, AES via `ICredentialProtector` | Versioned; re-encrypt in place |
| Access token | In-memory cache keyed by **connection id** | Expiry honoured, refresh-ahead margin |
| Event signing secret | `ISecretsManager`, per consumer | Rotatable without redeploy |

**The encryption key lives outside the database.** A backup containing both ciphertext
and key contains plaintext. Key backed up separately with its own access control.

Refresh tokens are **long-lived** and do not rotate on a timer. They need revocation,
re-consent and explicit rotation. Access tokens expire in about an hour — these are
different things and were conflated in revision 1.

**Assumption A1**: `AesCredentialProtector` is authenticated encryption with sound nonce
handling. If it is raw CBC, that is a defect to fix **before** it holds refresh tokens.

Multi-process refresh: the token cache is per process, so two workers may refresh
concurrently. That is acceptable (Amazon issues a valid token to each) provided neither
writes the access token to shared storage. What is **not** acceptable is a cache keyed by
region or provider rather than connection id — that leaks one tenant's token to another.

## 7. Migration plan — `Orders`, Phase 7 only

Phases 1–6 add tables only. Phase 7 is the single change to a live SPR path.

1. **Additive.** `ALTER TABLE "Orders" ADD "IntegrationConnectionId" uuid NULL;` Deploy. SPR unaffected — the column is unread.
2. **Backfill and verify.** Confirm every existing row has `TenantPartnerAccountId` set and the new column null. A row failing both is a pre-existing defect to resolve first.
3. **Constrain.** Add the check: exactly one of `TenantPartnerAccountId` / `IntegrationConnectionId` is non-null. Deploy separately, after step 2 verifies.
4. **Dispatch.** Introduce `IPurchaseOrderSink`; `SprOutboundOrderService` becomes one implementation. Selection comes from the order's own link, never a default to SPR.
5. **Regress.** Full EDI 850/855/856/810 round trip plus the four M360 message types, before merge.

**Rollback:** steps 1–3 reverse cleanly (drop constraint, then column) while no Amazon
Business orders exist. Once they do, rollback requires migrating those orders out first —
so the constraint and the first live purchase order should **not** ship together.

Per the repo's own history: declare defaults in the EF **entity configuration**, not only
`defaultValue:` in the migration; a rebaseline drops the latter. Any `List<string>` behind
a value converter needs a `ValueComparer`, or changes silently fail to persist.

## 8. Operational limits

The droplet shares PostgreSQL with Merchant360 on 8 GB with swap already in use.

- A capacity baseline and an objective gate **before** Phase 3 tenant activation.
- Per-tenant and per-connection feature flags; a kill switch per connection.
- Write operations behind a per-connection dry-run until the audit trail is trusted.
- Dead-letter queues with operator replay for outbound fulfilment and PC→M360 delivery.
- Alarms: connection status, reauthorisation due, sync lag, SQS backlog, throttle rate,
  webhook outbox age, unmapped SKU count, dead-letter depth.

---

## 9. Connection identity across reconnection (C3, v1.1)

**Guarantee: one Amazon order ⇒ exactly one M360 sales order, across disconnect,
reauthorisation and credential replacement.**

Stable identity is the Amazon **selling partner id** (`externalAccountId`), not the
PartnerConnect connection row.

| Event | Behaviour |
|---|---|
| Reauthorise an account already connected for this tenant | **Reuse the existing connection row.** New credentials replace old; `integrationConnectionId` is unchanged; watermarks and order history are preserved |
| Connect an account never seen for this tenant | New connection row |
| Connect an account currently `Revoked` for this tenant | **Reactivate that row**, do not create a second |
| Same Amazon account on a *different* tenant | Separate connection. Legitimate; isolation still applies |

Uniqueness: `(TenantId, IntegrationProviderId, ExternalAccountId)` unique **where
`ExternalAccountId IS NOT NULL`** — a partial index, because pending connections have no
account id yet and several may exist briefly.

The callback resolves identity **after** Amazon returns the selling partner id: if that id
already belongs to a connection for this tenant, the pending row is discarded and the
existing row reauthorised. Without this, every reconnect creates a new connection, the
new connection has no watermark, a full backfill re-imports every order, and
`(IntegrationConnectionId, ExternalOrderId)` no longer collides — **producing exactly the
duplicate M360 orders the contract forbids.**

Required tests: disconnect → reconnect; reauthorise while connected; revoke → reconnect;
same account on two tenants; account replaced by a genuinely different selling partner id.

## 10. Processing-result callback authorisation (C4, v1.1)

The processing-result endpoint is **model 2**, not webhook HMAC. It mutates PC state and
must be authorised as a service acting for an organization.

Validation, in order — each failure distinct:

| Check | Failure |
|---|---|
| Service credential valid | 401 |
| Caller's organization owns `tenantId` | 404 (not 403) |
| `pcSalesOrderId` exists and belongs to that tenant | 404 |
| `sourceEventId` was actually published for that order | 409 — a result for an event we never sent |
| Result already recorded for this `sourceEventId` | 200, idempotent, original retained |
| Conflicting result for the same event (`Processed` then `FailedPermanent`) | 409, first result wins, operator alerted |

A missing result is not an error — it is the reconciliation sweep's input. An order whose
last event was acknowledged but has no result after the threshold is surfaced on sync
health for operator attention.

**A 202 on the event endpoint is never a business outcome.** The only evidence that an
Amazon order became an M360 sales order is a `Processed` result carrying `m360SalesOrderId`.

## 11. Event identity: retry vs republication (C6, v1.1)

Three distinct things, previously blurred:

| | `eventId` | Trigger | Consumer effect |
|---|---|---|---|
| **Transport retry** | **Unchanged** | 5xx, 429, timeout | Inbox dedupes; second receipt returns 202 Duplicate; no reprocessing |
| **Business update** | **New** | Amazon reports a newer `sourceLastUpdatedAt` | Same order updated |
| **Manual republication** | **New** | Operator calls `/redeliver` | Same order updated, never a second order |

Rules:
- An automatic retry **must** reuse the original `eventId`. Minting a new one on retry
  defeats the inbox and risks double processing.
- A republication mints a new `eventId` but carries the **same `identity`**. Order
  identity governs; event id never does.
- `deduplicationKey` stays equal across retries of one transition and differs across
  genuine transitions.
- Retry exhaustion: the outbox row dead-letters, sync health shows it, and the operator
  can replay. Exhaustion is never silent.
- Ordering: `sourceLastUpdatedAt` then `sourceVersion`. An older snapshot is discarded,
  not applied. Equal timestamps are resolved by `sourceVersion`; if both are equal the
  payload is treated as identical and ignored.

## 12. SKU cross-reference synchronisation (C7, v1.1)

M360 owns the mapping. PC holds a cache so it can label an order line, never decide it.

| Concern | Rule |
|---|---|
| How PC obtains it | M360 pushes on change, and PC may read `GET /merchants/{id}/amazon/sku-mappings` on reconciliation |
| Precedence | `MerchantApproved` beats `FallbackMatch`, always. Never ASIN or title alone |
| Conflict | Two approved mappings for one `(connection, marketplace, sellerSku)` ⇒ 409; `Conflict` status; no inventory effect |
| Seller SKU changes | Treated as a **new** SKU needing approval. The old mapping is retained for historical orders so past orders keep their product |
| Unmapped | Order and line still created, flagged `UnmappedProduct`, no inventory effect |
| Mapping arrives later | Line is re-evaluated; the order is **not** re-imported |

Never: silently create a product, drop a line, or decrement inventory on an unverified
mapping.

## 13. Amazon API version dependencies (C5, v1.1)

**Order reads and shipment writes are on different API versions.** Verified against
Amazon's documentation on 2026-11-05:

| Operation | API | Version |
|---|---|---|
| `searchOrders`, `getOrder` | Orders | **v2026-01-01** |
| `confirmShipment` | Orders | **v0** — not listed under v2026-01-01 |

An Orders API upgrade does **not** carry the write operations with it. Phase 0 must
re-confirm whether a v2026-01-01 shipment-confirmation operation has since shipped, and
Phase 5 pins the exact version it calls.

`confirmShipment` field requirements — package detail, package reference id, carrier code,
tracking number, order items, and whether multiple packages and partial shipments are
supported — are **not established** from the overview page and must be read from the full
v0 reference before Phase 5. They are not assumed here.
