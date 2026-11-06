# Nine-phase implementation backlog

**Date:** 2026-11-06 · Sequence approved; not reordered here.
Owner column: **PC** PartnerConnect · **M360** Merchant360 · **PO** product owner ·
**AMZN** external dependency.

Complexity: **S** ≈ ½–1 day · **M** ≈ 1–3 days · **L** ≈ 3–5 days · **XL** ≈ 1–2 weeks.
Figures are build effort for someone who knows this codebase; they exclude review,
Amazon approval waits and pilot observation.

> Phase 1 is detailed task-by-task in `PHASE1_IMPLEMENTATION_INVENTORY.md` and
> `PHASE1_EXECUTION_CHECKLIST.md`. It is summarised here only for continuity.

---

## R0 — Security remediation (precedes Phase 1)

| # | Task | Owner | Cx | Depends on |
|---|---|---|---|---|
| R0.1 | Backup separation: `age`-encrypt the env tarball to a key the host does not hold | PC | M | Decision on `age` vs `gpg` |
| R0.2 | Tighten backup permissions — dirs 0700, dumps 0600 | PC | S | — |
| R0.3 | Rehearse restore with the custodian's private key | PC | S | R0.1 |
| R0.4 | Generate and install `CredentialEncryption__EncryptionKey` on both services | PC | S | **R0.1–R0.3**, operational approval |
| R0.5 | Fail-closed startup outside Development | PC | S | R0.4 deployed everywhere |
| R0.6 | Strict `Unprotect` for the new credential store only | PC | S | — |
| R0.7 | Corrected HMAC signer as shared code | PC | M | — |
| R0.8 | Outbox enqueue overload that joins a caller transaction | PC | M | — |
| R0.9 | Encrypt SPR `TransportCredentialsJson` (separate approval) | PC | S | R0.4 verified |
| R0.10 | Rotate SPR SFTP + web-service passwords | **PO + AMZN-equivalent (SPR)** | M | R0.9; SPR's schedule |

**Gate:** no fallback-key warning in any log; backup set yields no usable key; SPR regression
green; atomic commit/rollback proven on real PostgreSQL.

## Phase 0 — Decisions and contracts *(runs in parallel; does not block Phase 1)*

| # | Task | Owner | Cx |
|---|---|---|---|
| 0.1 | Submit Amazon seller developer registration | **PO** | S + wait |
| 0.2 | Submit Amazon Business programme application (published estimate 4–6 weeks) | **PO** | S + wait |
| 0.3 | Re-verify Orders API v2026-01-01 operations and roles against live docs | PC | S |
| 0.4 | Re-verify whether `confirmShipment` has a v2026-01-01 equivalent yet | PC | S |
| 0.5 | Read the full v0 `confirmShipment` reference — package detail, multiple packages, partial shipment | PC | S |
| 0.6 | M360 agrees the v1.1 event contract and receiver obligations | **M360** | M |
| 0.7 | Confirm US-only + FBM-first; billable metrics; seller onboarding model | **PO** | S |
| 0.8 | Resale/dropship policy and legal review | **PO** | M |
| 0.9 | Retention periods for buyer data and raw payloads | **PO** | S |

**Gate:** applications submitted; contract signed off; assumptions approved or explicitly deferred.

## Phase 1 — Provider substrate *(approved; start now)*

| Group | Tasks | Owner | Cx |
|---|---|---|---|
| Security remediation carry-in | R0.5, R0.6 applied | PC | S |
| Entities | `IntegrationProvider`, `IntegrationConnection`, `IntegrationCredential`, `IntegrationSyncState`, capabilities enum | PC | M |
| Persistence | 4 EF configs, partial unique index, repositories, `AddIntegrationSubstrate` migration | PC | L |
| Services | Connection service, credential store, context factory, adapter contracts | PC | L |
| API | `IntegrationsController` (4 routes), scopes, DTOs | PC | M |
| Observability | Audit, metrics, admin health view | PC | M |
| Tests | 10 matrix items + SPR regression | PC | L |

**Gate:** two connections for one tenant isolated; cross-tenant 404; credentials survive
restart; SPR green. **Total ≈ XL.**

## Phase 2 — Seller authorization

| # | Task | Owner | Cx | Depends |
|---|---|---|---|---|
| 2.1 | OAuth state store — single-use, hashed, expiring (model on `OrgPortalUserTokenService`) | PC | M | 1 |
| 2.2 | Authorize / callback / disconnect endpoints | PC | M | 2.1 |
| 2.3 | **Reconnect-reuse identity resolution (C3)** | PC | M | 2.2 |
| 2.4 | LWA token exchange + refresh with in-memory access-token cache keyed by connection | PC | M | 2.2 |
| 2.5 | Connection health, denied-capability reporting | PC | S | 2.4 |
| 2.6 | M360 connect/disconnect UI calling PC | **M360** | M | 2.2 |
| 2.7 | Tests — matrix 2.1–2.10 | PC | L | — |

**Gate:** a real seller connects, revokes and reconnects **without creating a second
connection**. Needs 0.1 approved for the live claim; mock gates can pass before.

## Phase 3 — Sales ingestion

| # | Task | Owner | Cx | Depends |
|---|---|---|---|---|
| 3.1 | `SalesOrder`, `SalesOrderLine`, `SalesOrderStatusHistory` + migration | PC | M | 1 |
| 3.2 | SP-API client — `searchOrders`, `getOrder`, `includedData` | PC | L | 0.3, 2 |
| 3.3 | Canonical mapper — raw status preserved, derived programs, money as decimal | PC | L | 3.1 |
| 3.4 | Rate limiter — per documented usage plan, multi-process safe | PC | L | 0.3 |
| 3.5 | Polling worker, watermark per capability per marketplace, overlap window | PC | L | 3.2 |
| 3.6 | **Pagination token expiry recovery** (24-hour expiry) | PC | M | 3.5 |
| 3.7 | SQS notification consumer — dedupe, delete after commit | PC | L | 3.2 |
| 3.8 | Reconciliation sweep | PC | M | 3.5 |
| 3.9 | **Scheduler fairness (A4)** — per-connection work, bounded concurrency | PC | M | 3.5 |
| 3.10 | **`RawDocumentArchive` remediation (A2)** — writer, redaction, retention, purge | PC | L | — |
| 3.11 | Sales-order read API + portal views | PC | M | 3.1 |
| 3.12 | Tests — matrix 3.1–3.15 incl. load | PC | XL | — |

**Gate:** orders persist exactly once across poll/notification overlap; PII and status cases
correct. **Capacity gate before activation** — the droplet shares PostgreSQL with M360 on
8 GB with swap in use.

## Phase 4 — PC → M360 exchange *(the phase that makes it real)*

| # | Task | Owner | Cx | Depends |
|---|---|---|---|---|
| 4.1 | Event publisher on the **transactional** outbox | PC | M | R0.8, 3.1 |
| 4.2 | Signed delivery with key id and rotation | PC | M | R0.7 |
| 4.3 | Processing-result endpoint with the six-step validation ladder | PC | M | 4.1 |
| 4.4 | Reconciliation + redelivery endpoints | PC | M | 4.3 |
| 4.5 | Event history / five observable states | PC | M | 4.1 |
| 4.6 | **Receiver + durable inbox + idempotency** | **M360** | L | 0.6, 4.2 |
| 4.7 | Create/update the operational sales order | **M360** | L | 4.6 |
| 4.8 | SKU mapping admin + unmatched handling | **M360** | L | 4.7 |
| 4.9 | Processing-result callback to PC | **M360** | M | 4.3 |
| 4.10 | Tests — matrix 4.1–4.17 | PC + M360 | XL | — |

**Gate:** one Amazon order ⇒ exactly one M360 sales order through duplicates, retries and
handler failure. **M360 is on the critical path here** — the only phase where it is.

## Phase 5 — Merchant-fulfilled execution

| # | Task | Owner | Cx | Depends |
|---|---|---|---|---|
| 5.1 | `FulfillmentSubmission` + seven-state machine | PC | M | 3.1 |
| 5.2 | `confirmShipment` client, version pinned | PC | M | 0.4, 0.5 |
| 5.3 | **Ambiguous-outcome reconciliation** — verify before any re-write | PC | L | 5.2 |
| 5.4 | Partial and multiple shipments | PC | M | 5.2 |
| 5.5 | Fulfilment API + dry-run flag per connection | PC | M | 5.1 |
| 5.6 | M360 fulfilment command + confirmation display | **M360** | L | 5.5 |
| 5.7 | Tests — matrix 5.1–5.11 incl. live pilot | PC | L | — |

**Gate:** an FBM order ships from M360, Amazon confirms **once**, a timeout recovers without
a duplicate. First phase that writes to a seller's live account — allowlisted pilot only.

## Phase 6 — Listings, inventory, pricing

| # | Task | Owner | Cx |
|---|---|---|---|
| 6.1 | Listings API client; listing ↔ catalogue linking | PC | XL |
| 6.2 | Inventory sync with **oversell guards** | PC + M360 | XL |
| 6.3 | Price sync, conflict precedence, channel pricing | PC + M360 | L |
| 6.4 | Suppression and eligibility error surfacing | PC | M |
| 6.5 | Rate-limited batching | PC | M |

**Gate:** a merchant publishes and manages listings without overselling.
**This is where "Amazon selling" becomes complete** — Phases 1–5 are order operations only.

## Phase 7 — Amazon Business purchasing

| # | Task | Owner | Cx | Depends |
|---|---|---|---|---|
| 7.1 | `AMAZON_BUSINESS` provider + buyer-context connection | PC | M | 1, 0.2 |
| 7.2 | **`Orders` migration** — nullable column → backfill/verify → check constraint | PC | M | — |
| 7.3 | `IPurchaseOrderSink`; `SprOutboundOrderService` becomes one implementation | PC | L | 7.2 |
| 7.4 | `placeOrder` client with deterministic external ids | PC | L | 0.2 |
| 7.5 | **Partial acceptance** — `acceptedItems` / `rejectedItems` → PO state | PC | M | 7.4 |
| 7.6 | `orderDetails` status ingestion; SNS path (separate from Seller SQS) | PC | M | 7.4 |
| 7.7 | Product Search integration | PC | L | 0.2 |
| 7.8 | Purchasing UI, approval and spend controls | **M360** | XL | 7.4 |
| 7.9 | **Full SPR EDI regression** | PC | M | 7.3 |

**Gate:** discover → purchase → partial acceptance → status, with SPR unchanged.
**Blocked by 0.2 and 0.8** regardless of engineering readiness. The only phase that
modifies a live SPR path.

## Phase 8 — Advanced operations

| # | Task | Owner | Cx |
|---|---|---|---|
| 8.1 | Settlement and financial reconciliation | PC + M360 | XL |
| 8.2 | Returns and refunds | PC + M360 | XL |
| 8.3 | FBA inventory operations | PC | L |
| 8.4 | Repricing | PC | L |
| 8.5 | Channel profitability reporting | **M360** | L |

---

## Critical path

```
R0.1-R0.3 ─► R0.4 ─► Phase 1 ─► Phase 2 ─► Phase 3 ─► Phase 4 ─► Phase 5
                                    ▲                    ▲
Phase 0 (PO/AMZN, parallel) ────────┘         M360 4.6-4.9 (critical path here)

Phase 7 ──► depends only on Phase 1-2 + 0.2 + 0.8; runs in parallel with 3-5
Phase 6, 8 ──► after 5
```

**Longest pole is not engineering.** Amazon Business onboarding (0.2) is a published 4–6 week
estimate and the resale policy review (0.8) has no engineering input at all. Both should be
started now; neither blocks Phase 1.

**M360 appears on the critical path once**, at Phase 4. If M360 capacity is uncertain,
that is the item to schedule first — PC can have Phases 1–3 finished and still deliver
nothing a merchant can use.

## Ownership summary

| Phase | PC | M360 | PO / external |
|---|---|---|---|
| R0 | all | — | SPR password rotation |
| 0 | API verification | contract sign-off | **applications, policy** |
| 1 | all | optional UI | — |
| 2 | all | connect UI | seller approval |
| 3 | all | — | capacity approval |
| 4 | publisher side | **receiver, inbox, SKU mapping** | — |
| 5 | submission + recovery | fulfilment command | pilot approval |
| 6 | provider sync | catalogue, inventory | — |
| 7 | connection + dispatch | buying UI | **programme access, policy** |
| 8 | ingestion | accounting | — |
