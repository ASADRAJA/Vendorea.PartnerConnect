# PartnerConnect ↔ Merchant360 Amazon Contract Package

**Version:** v1.1 (draft for review) · **Date:** 2026-11-05
**Status:** Awaiting review. Not approved. No implementation authorised.
**Supersedes:** v1.0 (2026-11-05). **Amends:** *Amazon Integration Architecture*, Revision 2 + errata C1–C8.

> **No production feature code was modified as part of this contract deliverable.**
> Everything here is a specification under `docs/amazon/`.

---

## 1. Scope

Defines the wire contract between PartnerConnect (PC) and Merchant360 (M360) for **Amazon marketplace selling**. Amazon Business purchasing is Phase 7 and reuses the existing supplier purchase-order APIs; only its dispatch extension is noted here, not re-specified.

| File | Contents | Owner repo |
|---|---|---|
| `PARTNERCONNECT_AMAZON_CONTRACT_INDEX.md` | This index, ownership matrix, decisions, open items | — |
| `partnerconnect-amazon-api-v1.openapi.yaml` | PC public API (connections, sales orders, fulfilment, reconciliation) | PartnerConnect |
| `partnerconnect-m360-events-v1.schema.json` | Event envelope + payload schemas PC publishes | PartnerConnect |
| `merchant360-partnerconnect-api-v1.openapi.yaml` | M360 event receiver + processing-result callback | Merchant360 |
| `AMAZON_CONTRACT_EXAMPLES.md` | Example messages and end-to-end sequences incl. failure paths | — |
| `AMAZON_CONTRACT_COMPATIBILITY_AND_SECURITY.md` | Auth, scopes, idempotency, versioning, PII, migrations | — |

## 2. Route and event ownership

Unambiguous: who implements which side.

| Surface | Implemented by | Called by |
|---|---|---|
| `GET/POST /api/v1/org/tenants/{tenantId}/integrations/**` | **PC** | M360 UI, org portal |
| `GET /api/v1/integrations/callback/{providerCode}` | **PC** | Amazon (browser redirect) |
| `GET /api/v1/org/tenants/{tenantId}/sales-orders/**` | **PC** | M360 reconciliation, org portal |
| `POST .../sales-orders/{id}/fulfillment` | **PC** | M360 |
| `POST .../sales-orders/{id}/redeliver`, `.../reconcile` | **PC** | M360 operator tooling |
| `POST /api/v1/integrations/partnerconnect/events` | **M360** | PC outbox worker |
| `POST .../sales-orders/{pcSalesOrderId}/processing-result` | **PC** | M360, after business processing |
| Existing `/api/integrations/orders/**` (supplier PO) | **PC** | M360 — **unchanged** |
| Existing `Merchant360OutboxMessageTypes` (4 types) | **PC** | M360 — **unchanged** |

**Identity rule.** Order identity is `(provider, integrationConnectionId, externalOrderId)`. `eventId` is the inbox deduplication key and is **not** an order identifier. These must never be conflated.

## 3. Decisions recorded in this package

1. **Primary business-processing feedback = M360 calls PC's processing-result endpoint.** Scheduled reconciliation is the backstop, not the primary path. Chosen over a PC-initiated poll because M360 knows when its handler finished; PC would otherwise poll blindly.
2. **Payloads are full snapshots, not deltas**, ordered by `sourceLastUpdatedAt` + `sourceVersion`. A consumer that misses an event converges on the next one.
3. **Delivery is at-least-once; business effects are effective-once** through the inbox. No exactly-once claim is made at the network level.
4. **Events carry least-necessary PII.** Buyer identity is referenced, not embedded, unless the capability requires it.
5. **Derived fields are labelled.** `isPrime`-style indicators are projections over `programsRaw`, never presented as Amazon source fields.

## 4. Unresolved external program / access dependencies

| Dependency | Owner | Impact if late |
|---|---|---|
| Amazon seller developer registration + role approval | Product owner / Amazon | Blocks Phase 2 onward. Mock gates still pass; no live claim possible |
| SP-API role grants for BUYER / RECIPIENT `includedData` | Product owner / Amazon | Phase 3 ingests orders without buyer data; Phase 5 FBM cannot ship |
| Amazon Business programme access (`AmazonBusinessOrderPlacement`) | Product owner / Amazon | Blocks Phase 7 entirely. Published estimate 4–6 weeks — **an Amazon planning figure, not a committed date**; confirm with the Amazon programme contact |
| Amazon Business Product Search role | Product owner / Amazon | Phase 7 ships order placement without catalogue search; degraded buyer UX |
| Resale / dropship policy and legal review | Product owner | Blocks Phase 7 use cases regardless of technical readiness |
| M360 engineering capacity for the receiver | M360 team | Blocks Phase 4 — the gate where Amazon orders become business objects |

## 5. Infrastructure verification results

The v1.0 assumptions were checked against the code. Full detail in
`INFRASTRUCTURE_VERIFICATION_REPORT.md`. Summary:

| # | Assumption | Verdict |
|---|---|---|
| A1 | `AesCredentialProtector` is AEAD | **Verified** — AES-GCM, random 12-byte nonce, 16-byte tag, versioned prefix |
| A1b | Encryption key configured in the deployed environment | **FAILED — critical.** Not set on pc-api or pc-workers; running on the hard-coded dev fallback key |
| A2 | `RawDocumentArchive` supports redaction/retention | **Missing.** Table and `ExpiresAt` exist; nothing writes it and no purge job exists |
| A3 | Outbox can be atomic with the order snapshot | **Requires modification.** `AddAsync` calls `SaveChangesAsync` itself; atomicity is incidental |
| A4 | Scheduler is fair across connections | **Requires modification.** Claim is per job, not per connection |
| A5 | Webhook signing reusable | **Requires modification.** Signs the body only; timestamp unsigned — the v1.0 C1 defect, already present in shipped code |
| A6 | Tenant resolution supports new endpoints | **Verified** — single choke point, 404 not 403 |
| A7 | PostgreSQL can enforce the identities | **Verified** — Testcontainers suite exists to prove constraints |

## 5b. Changelog — v1.0 to v1.1

| Ref | Change |
|---|---|
| C1 | Signature now covers `timestamp + "." + raw body`; `KeyId` header for rotation; constant-time compare; shared test vector |
| C2 | Three separate authentication models; SKU mapping moved to M360 user auth |
| C3 | Reauthorisation reuses the existing connection; partial unique index on `(tenant, provider, externalAccountId)` |
| C4 | Processing-result is service-authenticated with a six-step validation ladder |
| C5 | `confirmShipment` documented as Orders API **v0** — reads and writes are on different versions |
| C6 | Transport retry keeps `eventId`; republication mints a new one; `publicationReason` added |
| C7 | SKU cross-reference precedence, conflict and SKU-change behaviour defined |
| C8 | Consistency pass; automated validator added (`validate_contracts.py`) |

## 6. Version map

| Artefact | Version | Compatibility |
|---|---|---|
| PC Amazon API | `v1` | Additive only within v1; breaking change ⇒ `v2` path |
| PC→M360 events | `.v1` suffix per event type | Consumers ignore unknown fields; new required field ⇒ `.v2` |
| M360 receiver | `v1` | Must accept any `eventVersion` it declares support for |
| Existing supplier PO API | unchanged | Frozen; regression-tested |
| Existing 4 M360 message types | unchanged | Byte-compatible; asserted by contract tests |
