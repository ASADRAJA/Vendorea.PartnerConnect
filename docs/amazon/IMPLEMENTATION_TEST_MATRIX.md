# Implementation test matrix

**Date:** 2026-11-06 · Mapped to the nine-phase gates. Every requirement traces to at least
one executable test.

**Layers.** `U` unit (xUnit, no infrastructure) · `D` database (Testcontainers, **real
PostgreSQL**) · `I` integration (`TestWebApplicationFactory` + mock provider) · `C` contract
(schema/OpenAPI validation, wire-shape assertions) · `S` security/isolation · `L` load ·
`P` live pilot (allowlisted tenant, real Amazon).

**Standing rule — every phase.** The 175 existing unit tests stay green and the four
Merchant360 message types stay byte-identical. A phase whose gate passes while these fail
has not passed.

---

## R0 — Remediation (precedes Phase 1)

| # | Requirement | Layer | Test | Acceptance |
|---|---|---|---|---|
| R0.1 | Backup env tarball is unreadable on the host | S | Run backup; attempt `tar -tzf env.tar.gz.age` as root | Fails; `age -d` with the custodian key succeeds |
| R0.2 | Restore rehearsal with the custodian key | D | Restore `partnerconnect.dump` into a scratch DB; decrypt env separately | Both succeed independently |
| R0.3 | Backup permissions tightened | S | `stat` each set | Dirs 0700, dumps 0600 |
| R0.4 | Key round-trips across processes | D | Encrypt in `pc-api` context, decrypt in `pc-workers` context | Identical plaintext |
| R0.5 | Tampered ciphertext rejected | U | Flip one byte of the tag, `Unprotect` | `AuthenticationTagMismatchException`, never silent garbage |
| R0.6 | Fail-closed startup | I | Start with no key, `ASPNETCORE_ENVIRONMENT=Production` | Startup throws, reason logged |
| R0.7 | Development still permissive | I | Same, Development | Starts with the warning |
| R0.8 | Strict mode does **not** touch SPR | U | `Unprotect` legacy plaintext on the SPR path | Passthrough preserved |
| R0.9 | SPR survives key installation | I | Full SPR regression after 1B | EDI 850/855/856/810 round trip unchanged |
| R0.10 | SPR survives credential encryption (1C) | I | Same suite after `enc:v1:` value stored | Unchanged |
| R0.11 | Signature covers the timestamp | U | Alter only the timestamp, recompute | Signature differs — the A5 defect is gone |
| R0.12 | Shared test vector | C | Both repos compute the published vector | `a050819d…eedd0c` |
| R0.13 | Replay outside the window | U | Timestamp −6 minutes | 401 |
| R0.14 | Key rotation overlap | U | Sign with old id while new is primary | Both verify during overlap; old fails after retirement |
| R0.15 | Re-serialised body fails | U | Verify against reparsed JSON | Fails — proves raw-byte verification |
| R0.16 | Outbox atomic commit | **D** | Snapshot + enqueue in one transaction, commit | Both rows present |
| R0.17 | Outbox atomic rollback | **D** | Same, roll back | **Neither** row present |
| R0.18 | Legacy enqueue unchanged | D | Existing `EnqueueAsync` | Commits independently, as before |

## Phase 1 — Provider substrate

*Gate: two connections for one tenant isolated; credentials encrypted; SPR green.*

| # | Requirement | Layer | Test | Acceptance |
|---|---|---|---|---|
| 1.1 | Partial unique index on external account | **D** | Two `Pending` rows (null account) then two `Connected` rows, same account | Pending pair allowed; second connected rejected |
| 1.2 | `TenantId` non-nullable | D | Insert without tenant | Rejected by the database, not by code |
| 1.3 | Credentials never on a list path | U | Reflect over the list projection | `ProtectedValue` absent |
| 1.4 | Cross-tenant read | **S** | Tenant A requests B's connection | **404**, not 403 |
| 1.5 | Scoped user outside their tenant set | S | `TenantManager` scoped to tenant 1 requests tenant 2 | 404 |
| 1.6 | Viewer cannot write | S | Viewer calls create | 403 from scope policy |
| 1.7 | New scopes inherit role behaviour | U | `ScopesForRole` for all three roles | OrgAdmin + TenantManager hold `integrations:write`; Viewer does not |
| 1.8 | Two connections share no context | U | Build contexts for two connections in one process | Distinct credentials; no cached client reuse |
| 1.9 | Migration is additive | D | Apply to a copy of production schema | No existing table altered |
| 1.10 | SPR regression | I | Full suite | Unchanged |

## Phase 2 — Seller authorization

| # | Requirement | Layer | Test | Acceptance |
|---|---|---|---|---|
| 2.1 | State is single-use | U | Replay the same state | Second use rejected |
| 2.2 | State expiry | U | 11-minute-old state | Rejected |
| 2.3 | State tampering | S | Alter tenant id inside state | Rejected; no connection attached |
| 2.4 | **Cross-tenant attach** | **S** | Tenant A's state presented on tenant B's callback | Rejected, audited |
| 2.5 | Reconnect reuses the connection (**C3**) | **D** | Disconnect then reconnect same selling partner | Same `integrationConnectionId`; watermark preserved |
| 2.6 | Revoked reconnect reactivates | D | Revoke then reconnect | Row reactivated, not duplicated |
| 2.7 | Same account, two tenants | D | Connect one account under two tenants | Two rows; isolation intact |
| 2.8 | Account replacement | D | Reconnect with a different selling partner id | New connection; old history retained |
| 2.9 | Revocation stops work | I | Revoke mid-schedule | Jobs stop; status `Revoked` surfaced |
| 2.10 | Refresh token never logged | S | Grep logs after a full flow | No token material |

## Phase 3 — Sales ingestion

| # | Requirement | Layer | Test | Acceptance |
|---|---|---|---|---|
| 3.1 | v2026-01-01 mapping | U | Fixtures: pending, unshipped, partially shipped, shipped, cancelled, FBA, FBM, multi-line, multi-marketplace | Deterministic canonical output |
| 3.2 | Unknown provider status | U | Unrecognised value | `Status = Unknown`, raw preserved, operator signal — never coerced |
| 3.3 | **PENDING without PII** | U | Pending fixture, no buyer/address | `buyerDataAvailable: false`; **not** an error |
| 3.4 | Money precision | U | Nested `proceeds` | Decimal strings; no float; totals **not** asserted equal to proceeds |
| 3.5 | Duplicate order | **D** | Same order via poll and notification | Unique index collapses to one row |
| 3.6 | Out-of-order update | D | Older `sourceLastUpdatedAt` after newer | Discarded, not applied |
| 3.7 | Equal timestamps | D | Same timestamp, different `sourceVersion` | Higher version wins |
| 3.8 | **Watermark not advanced on failure** | D | Fail persistence mid-page | Watermark unchanged; re-poll recovers |
| 3.9 | **Pagination token expiry** | I | Expired `paginationToken` | Restart from a safe overlap window; no loss |
| 3.10 | SQS redelivery | I | Same notification twice | Processed once |
| 3.11 | SQS delete after commit | I | Fail commit after receipt | Message **not** deleted; redelivered |
| 3.12 | Throttling | I | Provider returns 429 | Backoff with jitter; no tight loop |
| 3.13 | **Tenant fairness** | **L** | One connection throttled among many | Others keep progressing |
| 3.14 | Multi-process limiter | L | Two worker processes | Combined rate within the plan |
| 3.15 | Buyer data retention | S | Archive after retention window | Redacted or purged per policy |

## Phase 4 — PC → M360 exchange

*Gate: exactly one M360 sales order per Amazon order, through every failure path.*

| # | Requirement | Layer | Test | Acceptance |
|---|---|---|---|---|
| 4.1 | **One order, one M360 record** | I | Duplicate poll + notification + webhook retry | Exactly one M360 order |
| 4.2 | Snapshot and event atomic | **D** | Roll back the transaction | No orphan event |
| 4.3 | Transport retry keeps `eventId` (**C6**) | U | Retry after 500 | Same `eventId`; inbox dedupes |
| 4.4 | Republication mints a new id | U | `/redeliver` | New `eventId`, same identity, same order updated |
| 4.5 | Signature rejected | S | Bad signature at M360 | 401; no inbox write; PC does not retry |
| 4.6 | **202 then handler failure** | I | Accept, then fail processing | Result `FailedRetryable`; no duplicate order; surfaced on sync health |
| 4.7 | Handler ok, callback fails | I | Suppress the callback | Reconciliation resolves; no duplicate |
| 4.8 | Processing-result authorisation (**C4**) | S | Wrong org/tenant | 404; unknown `sourceEventId` → 409 |
| 4.9 | Processing-result idempotent | I | Same result twice | 200; original retained |
| 4.10 | Conflicting result | I | `Processed` then `FailedPermanent` | 409; first wins; alert |
| 4.11 | Retry exhaustion | I | Exceed the budget | Dead-letter; visible; replayable |
| 4.12 | **Unmapped SKU** | I | Unknown seller SKU | Order created, line flagged, **no inventory effect** |
| 4.13 | Mapping conflict | D | Two approved mappings | 409; `Conflict`; no inventory effect |
| 4.14 | Mapping arrives later | I | Map after import | Line re-evaluated; order **not** re-imported |
| 4.15 | Seller SKU changed | D | SKU renamed upstream | Treated as new; old mapping retained for history |
| 4.16 | **Supplier topics untouched** | **C** | Inspect emitted types | No marketplace event on `OrderStatus` |
| 4.17 | **M360 wire contracts** | C | `Merchant360WireContractTests` | Four types byte-identical |

## Phase 5 — FBM execution

| # | Requirement | Layer | Test | Acceptance |
|---|---|---|---|---|
| 5.1 | FBA rejected for fulfilment | I | Submit against an FBA order | 422 |
| 5.2 | FBA never queued | I | Ingest FBA order | Absent from the pick/pack queue |
| 5.3 | Idempotency replay | I | Same key twice | Original submission returned; one provider call |
| 5.4 | Key persisted **before** the call | D | Crash between persist and call | Submission exists in `Requested` |
| 5.5 | **Timeout → OutcomeUnknown** | I | Provider times out | State `OutcomeUnknown`; **no automatic retry** |
| 5.6 | Verify before re-write | I | Reconcile an unknown outcome | Already-shipped → `Confirmed`; not shipped → `RetryableFailure` |
| 5.7 | Partial shipment | U | Subset of lines | Quantities correct; order partially fulfilled |
| 5.8 | Multiple shipments | I | Two shipments, one order | Both recorded; no double-count |
| 5.9 | Over-ship rejected | U | Quantity > ordered | 422 |
| 5.10 | `confirmShipment` version pinned | C | Assert the client's version constant | Orders **v0** until re-verified |
| 5.11 | Live pilot | **P** | Allowlisted tenant, real order | Amazon confirms once; audit complete |

## Phases 6–8 — outline

| Phase | Focus | Representative must-pass |
|---|---|---|
| 6 — Listings, inventory, pricing | Oversell prevention | Concurrent inventory updates never oversell; price conflict precedence honoured; suppression surfaced |
| 7 — Amazon Business | Purchasing without disturbing SPR | **Partial acceptance** (`acceptedItems`/`rejectedItems`) mapped correctly; `Orders` check constraint rejects both-null and both-set (**D**); full SPR EDI regression green; ambiguous `placeOrder` reconciled before resubmission |
| 8 — Advanced operations | Financial reconciliation | Settlement figures reconcile; returns and refunds do not corrupt order state |

---

## Coverage by gate

| Phase | Tests | Blocking layers |
|---|---|---|
| R0 | 18 | D, S, I |
| 1 | 10 | D, S |
| 2 | 10 | S, D |
| 3 | 15 | U, D, I, L |
| 4 | 17 | I, D, C, S |
| 5 | 11 | I, D, P |

## Rules

1. **InMemory never proves a constraint.** Uniqueness, check constraints and transactions run on real PostgreSQL or they are not tested.
2. **A sandbox response is not a live validation.** `P` tests are separate and named as such.
3. **Security tests assert 404, not 403**, wherever tenant scope is the reason.
4. **Every phase re-runs R0 and the SPR regression.** Remediation does not stay fixed by itself.
