# Runway Without Amazon Credentials

**Date:** 2026-11-10

## The short answer

**Everything through the end of Phase 4.** That is not a technicality — Phase 4 is the phase that
makes the integration real, and it has **no Amazon dependency at all**. Its counterparty is
Merchant360.

The wall is not where it looks. It is not "we cannot build order handling without Amazon"; it is a
small, specific set of values that fail *silently* if guessed, plus every gate that requires a
real seller.

## Fully completable now

| Item | Why it is unaffected |
|---|---|
| 3.8 Reconciliation sweep | Logic over our own data |
| 3.10 `RawDocumentArchive` remediation (A2) | Our storage, our retention policy |
| 3.11 Sales-order read API + portal views | Our API |
| 3.12 Remaining Phase 3 tests | Stubs, as Phases 2–3 already use |
| **4.1 Event publisher on the transactional outbox** | PC → M360 |
| **4.2 Signed delivery with key id and rotation** | PC → M360; the signature contract is already agreed and test-vector-matched |
| **4.3 Processing-result endpoint, six-step validation ladder** | M360 → PC |
| **4.4 Reconciliation + redelivery endpoints** | Our API |
| **4.5 Event history, five observable states** | Our data |
| 4.10 Phase 4 tests | Both sides stubable |
| 5.1 `FulfillmentSubmission` + seven-state machine | Our model |
| 5.5 Fulfilment API + per-connection dry-run flag | Our API |
| Entire deployment/security track | Gated on operational approval and custodian naming, not Amazon |

**Phase 4 is also the critical path.** It is the only phase where Merchant360 is on it (4.6–4.9:
receiver, durable inbox, operational sales order, SKU mapping, processing-result callback). PC can
finish 4.1–4.5 and still deliver nothing a merchant can use until that lands — so the waiting
period is the right time to be doing it, and the right time for M360 capacity to be scheduled.

## Buildable but not verifiable

| Item | Built against | Cannot confirm |
|---|---|---|
| 3.7 Notification consumer | Published `ORDER_CHANGE` payload shapes | The real queue. `createDestination`/`createSubscription` need a registered application, so there is no queue to subscribe to |
| 5.2 `confirmShipment` client | Orders **v0**, which is fully documented | That Amazon accepts our request shape |
| 5.3 Ambiguous-outcome reconciliation | Our own state machine | Real timeout behaviour |

These are worth building — the Orders client was built this way and the approach held up — but each
carries assumptions that only live contact retires.

## The hard wall

Six things cannot be done at all, and four of them fail *silently* rather than loudly, which is
why they are not being guessed at:

| Blocked | Why |
|---|---|
| **3.13 Restricted Data Token** | The `path` inside `restrictedResources` is undocumented for the v2026-01-01 endpoint. A wrong path yields a token that authorises nothing, with no error |
| **`proceeds.breakdowns[].type` vocabulary** | Amazon types it as a bare string without enumerating values. Four derived money columns depend on it; wrong matching produces plausible wrong numbers |
| **Phase 2 gate** | "A real seller connects, revokes and reconnects without creating a second connection" |
| **Phase 3 gate (live half)** | Orders persisting exactly once across a *real* poll/notification overlap |
| **Phase 5 gate** | An FBM order shipping from a seller's live account — allowlisted pilot only |
| Phase 6, 7 | Listings/Feeds and Amazon Business, the latter needing its own 4–6 week onboarding |

### The sandbox does not help

SP-API sandbox access is granted **through the Solution Provider Portal after registering**. It is
not an alternative to registration; it is one of the things registration unlocks.

## Recommended sequence

1. **Finish Phase 3's non-Amazon half** — 3.10 first, because `RawDocumentArchive` governs buyer
   data and buyer data is what the restricted role will eventually bring in. Then 3.8, 3.11, 3.12.
2. **Build Phase 4 complete on the PC side** — 4.1 through 4.5, with tests.
3. **Stop before Phase 5's Amazon-facing work.** 5.1 and 5.5 are ours and can be done; 5.2 and
   5.3 should wait rather than accumulate more unvalidated surface.

That is weeks of productive work, and it is the highest-value work available: Phase 4 is where a
Merchant360 merchant first sees an Amazon order.

## The counterweight, stated plainly

One pass over Amazon's live documentation produced **four design-changing findings** and uncovered
**one gap in code already written**. Phases 1–3 have been built with zero live Amazon contact.
Phase 4 is safe to build blind because its contract is with Merchant360 and already agreed; Phase
5's Amazon-facing parts are not, which is where the boundary above comes from.

Registration is also pure lead time with no engineering input — particularly the restricted-role
data-protection review. Starting it costs nothing and shortens Phase 5's critical path.
