# Amazon Registration — What to Request, and What It Unblocks

**Date:** 2026-11-06. Prepared because registration is being started.

## 1. Register on the Solution Provider Portal, not as a seller

**You do not need a Professional selling account.** Registering as a developer through the
Solution Provider Portal requires no selling account, which is the correct path for Vendorea and
the only one consistent with the role we have just written into the schema: Vendorea is a software
and integration provider and never a merchant of record. Opening a seller account to get API
access would have been both unnecessary and contrary to that.

The Solution Provider Portal is replacing Developer Central as the gateway for new SP-API
developers — registration, sandbox access, application creation and usage metrics all live there.

Three Amazon developer/solution-provider policies took effect **25 August 2026** and apply to any
application touching SP-API. Worth reading before submitting rather than after.

## 2. Roles to request

Amazon's current role list has 24 entries, five of them restricted. These are the four that
matter to us:

| Need | Role | Restricted? |
|---|---|---|
| Read orders, statuses, line items | **Inventory and Order Tracking** | No |
| Buyer name/email, shipping address, **and** `confirmShipment` | **Direct-to-Consumer Shipping** | **Yes** |
| Order and line financials (`includedData=PROCEEDS`) | **Finance and Accounting** | No |
| Listings, inventory, pricing (Phase 6) | **Product Listing**, **Pricing** | No |

**Confirm these four against the application form rather than taking them from me.** The role
list is verified; the mapping of role to capability is my reading of it, and the documentation
summary I pulled contradicted itself on one row (it put buyer address under *Tax Invoicing*,
which is for issuing tax invoices, not shipping). A wrong role request costs a re-review cycle,
so it is worth five minutes on the form.

**Tax Invoicing and Tax Remittance are not needed** unless Merchant360 will issue Amazon tax
invoices — a separate decision, not part of Phases 3–5.

### What each role unblocks, concretely

| Without it | Consequence |
|---|---|
| Inventory and Order Tracking | Nothing works at all |
| Finance and Accounting | Orders ingest with **no monetary values** — a working ingestion that cannot produce a sellable Merchant360 order |
| Direct-to-Consumer Shipping | Orders ingest with `buyerDataAvailable: false` and **Phase 5 cannot ship anything** — there is no address |

The restricted role additionally requires a data-protection and security attestation about how
PII is handled, which is the part with a review delay attached. It is also the gate on backlog
item 3.13 (Restricted Data Tokens), without which buyer and address data stay unreadable even
once the role is granted.

## 3. What I need from you when it is done

| Value | Goes to |
|---|---|
| Application id | `Amazon:Lwa:ApplicationId` |
| LWA client id | `Amazon:Lwa:ClientId` |
| LWA client secret | `Amazon:Lwa:ClientSecret` — protected configuration only, never appsettings |
| Registered redirect URI | `Amazon:Lwa:RedirectUri`, and it must match **exactly** |
| Draft or published | `Amazon:Lwa:DraftApplication` — a draft app's consent URL needs `version=beta`, a published one rejects it |
| Which roles were approved | Determines `Amazon:SpApi:IncludedData` |

The redirect URI to register is:

```
https://pcapitest.vendorea.com/api/v1/integrations/callback/AMAZON_SP
```

Substitute the actual API host. Amazon rejects the token exchange if this differs from
`RedirectUri` by even a trailing slash, and that failure reads as a generic invalid-grant error.

## 4. What is blocked until then, and what is not

### Not blocked — building continues

| Item | Why it can be built blind |
|---|---|
| 3.7 Notification consumer | Payload shapes are published; verified the same way the Orders client was |
| 3.8 Reconciliation sweep | Pure logic over our own data |
| 3.10 `RawDocumentArchive` remediation (A2) | Our storage, our retention policy |
| 3.11 Sales-order read API and portal views | Our API |
| 3.12 Remaining tests | Stubs, as Phases 2 and 3 already use |
| Phase 4 planning | Contract already agreed with Merchant360 |

### Blocked — and deliberately not guessed at

| Item | What is missing |
|---|---|
| **3.13 Restricted Data Token** | The `path` value inside `restrictedResources` is undocumented for the v2026-01-01 endpoint. A wrong path yields a token that silently authorises nothing, so it gets confirmed live |
| **`proceeds.breakdowns[].type` vocabulary** | Amazon types it as a bare string without enumerating values. Four derived money columns depend on it and currently match loosely, preserving the raw blob. Must be confirmed against a real seller response before Phase 4 publishes money to Merchant360 |
| **Phase 2 gate** | "A real seller connects, revokes and reconnects without creating a second connection" needs an approved application |
| **End-to-end validation of anything** | Phases 1–3 have been built with zero live Amazon contact |

## 5. Why to start now anyway

One verification pass against the live documentation produced **four design-changing findings**
and uncovered **one gap in code already written** (the missing RDT flow). That is the yield from
reading documentation. Contact with the actual API will produce more, and every week of blind
building adds assumptions that have to be revalidated rather than merely tested.

The restricted-role review is the long pole and has no engineering input at all. Submitting it
early costs nothing and shortens the critical path for Phase 5.
