# SPR Colon Accounts — Requirements and Findings

**Date:** 2026-11-12
**Status:** requirements captured. **No design decided, no code changed.**
**Purpose:** record what the requirement is, what the current system actually does, and which side
owns which part — before any design work starts.

---

## 1. The account model

A dealer holds **one customer account group** at S.P. Richards and **one or more accounts** within
it, distinguished by the two digits after the period. One is the default.

```
0033822          ← customer account group
0033822.01       ← account
0033822.02       ← account
0033822.55       ← account
```

Group and account are therefore **two different identifiers**. PartnerConnect currently stores a
single string per tenant and uses it for both.

---

## 2. Requirements, as stated

| # | Requirement |
|---|---|
| R1 | A dealer can have more than one colon account with SPR. |
| R2 | Each account can have its own price file. |
| R3 | The price shown on the storefront comes from the **default** account. |
| R4 | While processing an order, the dealer can ask, **on demand**, what an item costs under a different colon account. |
| R5 | When placing an order, the dealer selects which colon account to place it under. |
| R6 | The dealer can give each account a nickname, or leave it as the number. |
| R7 | One colon account per order to SPR. Ordering some items under a different account therefore **splits** the order, and the splits must still trace back to the original customer order. |
| R8 | Not every account's price file contains every item in the default account's file. The system must check the account's price file to decide whether an item can be ordered under that account. |

### Decisions already given

| Question | Answer |
|---|---|
| An account with no price file? | **The dealer cannot order through it.** |
| Who performs the split and holds the parent? | **Merchant360 splits and keeps the record.** |
| The buyer saw the default price; the dealer orders under a different account at a different cost | **The difference is the dealer's margin. No visibility to the customer.** |
| Does availability differ by account? | **Yes** — confirmed, because the stock-check request already carries an account (see §3). |

---

## 3. What the current system actually does

Established by reading the code and querying the test database on 2026-11-12. Evidence, not
assumption.

### Already in place and usable

| Fact | Where |
|---|---|
| A tenant may already hold several accounts for one partner | unique index `(TenantId, TradingPartnerId, AccountNumber)` on `TenantPartnerAccounts` |
| A default flag exists | `TenantPartnerAccount.IsDefault` — present, unused |
| A nickname field exists (**R6**) | `TenantPartnerAccount.DisplayName` — present, null in all live rows |
| Orders already associate to an account | `Order.TenantPartnerAccount` |
| The stock check already sends an account | `SprSoapConfig.CustNumber`, sent in three places by `SprInteractiveServicesClient` |
| `DealerStockCheck` returns **dealer net price** live from SPR | `ISprInteractiveServices` — "availability across all DCs + dealer net price (needs CustNumber)" |

### Gaps that activate the moment a second account exists

| Gap | Detail |
|---|---|
| The account is chosen arbitrarily | `SprWebServiceContextResolver` uses `FirstOrDefault(...)` and never consults `IsDefault`. Harmless with one account; with three, which account a lookup goes out under depends on query order |
| Group and account receive the same value | `CustNumber = AccountNumber`, and `GroupCode` falls back to `AccountNumber` when the partner-level code is blank. Under this model the group is `0033822` and the account is `0033822.00` |
| Price data is keyed one level too coarse | `PriceFeedUpload.DealerId` and `SprPriceRecord.DealerId` are the **tenant**. Nothing records which account a price file belongs to |
| Duplicate detection would block a legitimate file | the hash scope is `(dealerId, partnerId, fileHash)`. Two accounts with identical pricing produce identical files, and the second would be rejected as a duplicate |
| Nothing binds a price file to an account | `dealerId` is a query-string parameter on upload; the parser reads no account identifier out of the file. Choosing wrongly is now choosing between a dealer's **own** accounts — more likely, and much harder to notice |
| `SpecialIdentifyingCode` duplicates `AccountNumber` | `AdminTenantsController` sets both to the same value |

### A useful constraint on the size of the change

Reads already work in two steps — find the latest upload, then filter records by
`PriceFeedUploadId`. If "latest upload for dealer" becomes "latest upload for (dealer, account)",
the record-level queries do not change.

**The account dimension therefore belongs on `PriceFeedUpload` only.** Records inherit it through
the upload. Denormalising it onto `SprPriceRecord` would duplicate the truth for no gain.

### Live data: no corruption

| Observation | |
|---|---|
| Both test tenants map to the same account `0033822.00` | accepted as test-environment only; no orders are placed with SPR |
| Dealer 2 holds two uploads, 47,346 (Aug) and 46,585 (Oct) rows | **two generations of one account's file, not two accounts** — of 45,501 shared SKUs, 40,955 (90%) have identical costs in all three cost columns |
| 1,845 stock numbers present in August vanished from October | PC stops reporting them correctly; Merchant360 was never told to retire them (see §6) |

### The undocumented status field — blocking R8

`SprPriceRecord.ProductStatus` is parsed from **column 5 of the price CSV** (`GetString(columns, 4)`).
Across all 141,277 live rows it holds **exactly one single character per row**, from a four-value
domain, with no nulls or blanks:

| Value | Rows (all uploads) | Rows (Oct upload) |
|---|---|---|
| `C` | 61,759 | 3,731 |
| `U` | 58,057 | 36,459 |
| `D` | 16,539 | 5,203 |
| `A` | 4,922 | 1,192 |

**Nothing in the codebase reads this field.** The meanings are unknown — these are observations
from the data, not documentation. The distribution shifts markedly between files, so the codes are
a live per-item attribute rather than a static classification.

`NewItemNumber` (column 6) is **empty on every row**, so it offers no clue. (An earlier reading
suggesting otherwise was a quoting error in the query and is withdrawn.)

**R8 needs only one answer from SPR:** *for each value of the column-5 status field, can the item
be ordered?* Presence in a price file is not sufficient — if `D` means discontinued, 11% of the
live default file would pass a naive presence check and fail at SPR.

**Owner: dealer / SPR. Outstanding.**

---

## 4. Ownership — PartnerConnect

PartnerConnect is the authority on **accounts, prices, orderability, and everything SPR-facing**.

| # | Work | Notes |
|---|---|---|
| R1 | Honour `IsDefault` in the resolver instead of `FirstOrDefault` | fixes a latent bug regardless of this project |
| R1 | Derive `GroupCode` from the account (the part before the period) and keep `CustNumber` as the full account | depends on the `.00` question in §6 |
| R1, R6 | Manage accounts per tenant: add, set default, set nickname | `AdminTenantsController` already has the shape |
| R1, R6 | **Expose the dealer's account list** — number, nickname, default flag, whether a price file exists | new contract surface; Merchant360 consumes it |
| R2 | Account dimension on `PriceFeedUpload` + migration | one nullable column; backfill is trivial here (one account per tenant today) |
| R2 | Upload takes an account, not a bare `dealerId`, and validates it belongs to the tenant | closes the "nothing binds a file to an account" gap |
| R2 | Duplicate-detection hash scope gains the account | one line |
| R2 | Repository reads become "latest upload for (dealer, account)" | ~10 methods; every one must be found |
| R3 | Push **only the default account's** price set to Merchant360, and record which account it came from | the push itself stays one set per dealer |
| R4 | **Price query endpoint**: cost of SKU under a given account, on demand | design open — file read or live `DealerStockCheck`; see §6 |
| R8 | **Orderability endpoint**: can SKU X be ordered under account Y | presence in that account's latest file **and** an allowed status code |
| R8 | Price-file staleness signal | R8 makes file currency a correctness input to ordering; nothing reports a stale file today |
| R5, R7 | Accept the ordering account on an inbound order; validate it belongs to the tenant and has a price file | reject rather than fall back to the default |
| R7 | One EZPO4 PO per split, each under its own account, **never merged** | PC must not recombine what Merchant360 split |
| R7 | Carry Merchant360's parent-order reference on each split, and **echo it on every callback** | POACK / ASN / invoice correlation must resolve to the right split and report upward |
| — | Availability per account | pass the selected account as `CustNumber`; the field already exists |

## 5. Ownership — Merchant360

Merchant360 owns the **customer order, the split, and every dealer- and buyer-facing screen**.

| # | Work | Notes |
|---|---|---|
| R3 | Storefront price continues to come from the pushed default set | no change if PC keeps sending one set per dealer |
| R3 | Record which account a displayed price came from | needed later to explain a margin difference |
| R4 | Call PC's price query during order processing and show the result | UI |
| R5 | Order-entry selection of the colon account | per order, or per line if a split is to be offered at line level — **open, see §6** |
| R6 | Display nicknames wherever accounts are shown, falling back to the number | |
| R7 | **Perform the split** and hold the parent customer order | one customer order → N SPR orders, one per account |
| R7 | Aggregate split status back to the parent | one split shipped and one backordered is a state the customer order must express |
| R7 | Send each split to PC with its account **and** the parent reference | |
| R8 | Block or warn on selecting an account under which an item cannot be ordered | calls PC's orderability check before committing |
| R3 | Keep the cost difference invisible to the customer | the margin stays with the dealer |

## 6. Open items

### Blocking

| # | Item | Owner |
|---|---|---|
| 1 | **Column-5 status codes.** For each of `A`/`C`/`D`/`U`, can the item be ordered? R8 cannot be implemented correctly without this | dealer → SPR |
| 2 | **Is `.00` an account or the group?** Live data stores `0033822.00`; the stated examples begin at `.01`. This decides whether the group is simply the part before the period, or whether `.00` is a special case | dealer → SPR |

### Design decisions, not yet made

| # | Item |
|---|---|
| 3 | **R4: file read or live call?** `DealerStockCheck` already returns dealer net price from SPR per account. A live call is current but costs a round trip and depends on SPR being available; the file is instant but only as fresh as the last upload. A plausible split is live for the authoritative price at order time, file for catalogue breadth and the R8 check |
| 4 | **Is the account selected per order or per line?** R7 implies per line is possible, since "one of many items" under a different account causes a split. Per-line selection is a materially larger piece of UI and of order modelling than per-order |
| 5 | **Freight minimums on splits.** Two POs of $150 may each miss a minimum that one $300 order met. Splitting has a commercial cost, and the dealer should see it at selection rather than on the invoice |
| 6 | **Retirement across uploads.** 1,845 SKUs vanished between the August and October files. `PriceBatchRequest` has no replacement or retirement semantics, so whether Merchant360 retires absent items depends on undocumented behaviour. Per-account price sets make this a per-account question |
| 7 | **No price file for the default account.** R3 says the storefront price comes from the default. If the default has no current file, what does the storefront show? |

---

## 7. Explicitly not decided

No schema change, no contract change, no endpoint, and no migration has been designed or written.
The size estimates in §4 are observations about where the work would land, not a plan.

The contract between PartnerConnect and Merchant360 will need new surface for R1, R4, R5, R7 and
R8 — an account list, a price query, an orderability check, an account and parent reference on
inbound orders, and the parent reference echoed on callbacks. That is a versioned contract change
and should be agreed in writing before either side builds against it, the same way the Amazon
contract package was.
