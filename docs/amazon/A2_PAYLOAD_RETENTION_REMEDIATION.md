# A2 Remediation — Payload Archive, Redaction and Retention (item 3.10)

**Date:** 2026-11-11
**Finding A2 was:** *"`RawDocumentArchive` supports or can be extended for PII redaction,
retention, and deletion"* — classified **missing**, because the table has no writer.

---

## 1. `RawDocumentArchive` could not be extended, and was left alone

The remediation plan assumed extension. Reading the configuration says otherwise:

```csharp
builder.HasIndex(x => x.PartnerDocumentId).IsUnique();
builder.HasOne(x => x.PartnerDocument)
    .WithOne(x => x.Archive)
    .HasForeignKey<RawDocumentArchive>(x => x.PartnerDocumentId);
```

A strict one-to-one on a **non-nullable** key. Holding an Amazon order in it would mean either
inventing `PartnerDocument` rows for payloads that are not documents, or making the key nullable
and giving up the invariant that keeps the SPR pipeline honest. Neither is worth doing to avoid
creating a table.

**`RawDocumentArchive` is therefore untouched, and remains unused.** Building its writer would add
storage growth to the SPR pipeline that nobody asked for, and the standing instruction is to
preserve SPR behaviour rather than extend it. It stays a known gap in the *supplier* pipeline,
recorded here so it is not mistaken for part of this work.

## 2. What was built instead

### `IntegrationPayloadArchive` — redacted, expiring, deduplicated

| Property | Decision |
|---|---|
| **Redaction** | Before the write. There is no window in which the table holds an unredacted copy, not even briefly |
| **Expiry** | `ExpiresAt` is **non-nullable**. A nullable expiry is how an archive becomes permanent: one path forgets to set it and those rows are never collected |
| **Dedupe** | By content hash per reference. Polling re-reads an overlap window every cycle, so without this the table fills with identical copies of unchanged orders faster than with anything useful |
| **Size cap** | 256 KB. One pathological response should not fill the table; over the cap is logged as skipped, not stored silently |
| **Failure** | Swallowed. The archive observes an ingestion; failing the ingestion because the observation failed is the wrong trade every time |
| **Column type** | `text`, not `jsonb`. `jsonb` normalises key order and drops duplicate keys, which would stop this being a record of what arrived |

Captured per order, not per page — the question is always about one order, and a page-level
capture means grepping through a hundred of them. Provider error bodies are captured too, under
the same redaction and the same expiry.

### Redaction matches property **names**, not paths

The important design choice. A path list (`recipient.deliveryAddress.addressLine1`) reads more
precisely and fails **silently** the moment the provider moves a field — which Amazon has just
done: v0's flat `ShippingAddress` became v2026-01-01's nested `recipient.deliveryAddress`. A
redactor pinned to the old paths would have gone on reporting that it redacted nothing.

Name matching also catches fields that do not exist yet: `buyer.secondaryEmail` is already
covered by the substring rule. Both cases are tested.

| Removed | Kept |
|---|---|
| `buyerName`, `buyerEmail`, `buyerCompanyName`, `buyerPurchaseOrderNumber`, tax ids | `city`, `stateOrRegion`, `countryCode`, `districtOrCounty` |
| `name`, `companyName`, `addressLine1-3`, `postalCode`, `phone` | `orderId`, statuses, `marketplaceId`, money, programs |
| `streetName`, `streetNumber`, `complement`, `neighborhood`, `municipality` | SKUs, ASINs, titles, quantities |
| Anything containing `email` or `phone` | `channelName`, `marketplaceName` — compounds, not personal |

`name` is listed bare because in this payload the innocuous ones are all compounds. Over-redaction
is the safe direction of error: the archive answers *"what shape did this arrive in, and what did
it say?"*, which needs no buyer's street.

`RedactedPaths` records what was removed. "The address is missing" and "we redacted the address"
look identical in the stored document and mean entirely different things when working out why an
order would not ship.

### Buyer-data retention on the order row itself

The archive holds no personal data; the **order** does, because a merchant-fulfilled order cannot
be shipped without it. `BuyerDataRetentionJobHandler` is what stops that being permanent.

**Two thresholds, because one cannot be right:**

| Threshold | Default | Applies to |
|---|---|---|
| `SettledOrderWindow` | 90 days | `Fulfilled` and `Cancelled` orders only |
| `HardLimit` | 180 days | Any order, whatever its status |

Redacting an order still awaiting shipment destroys the address it needs — correct privacy
behaviour producing an undeliverable order. But without the outer limit, an order stuck outside a
terminal state would hold personal data for ever, and the policy would silently depend on a state
machine always completing. Reaching the hard limit unsettled is logged as a **warning**: an order
that old which has not settled is itself a problem, and redacting it quietly would remove the
evidence along with the address.

`RedactPii` keeps `ShipToCountryCode` — not identifying alone, and tax and reporting need it — and
stamps `PiiRedactedAt`, which the sweep never rewrites. That timestamp is the only record of when
data was erased.

Retention runs **across tenants**, deliberately unscoped. It is a platform obligation; scoping it
per tenant would make it an obligation nobody was responsible for discharging.

## 3. Every window here is provisional

The approved contract records retention as an **open decision**, dependent on Amazon's data
policies for the roles actually granted, to be set deliberately rather than defaulted. The
defaults above are chosen to be short and defensible, not authoritative. They exist so that
nothing is ever stored without an expiry.

| Setting | Default | Status |
|---|---|---|
| `Integrations:PayloadArchive:Retention` | 30 days | provisional |
| `Integrations:BuyerDataRetention:SettledOrderWindow` | 90 days | provisional |
| `Integrations:BuyerDataRetention:HardLimit` | 180 days | provisional |

## 4. A second instance of the A2 failure, found and fixed

Handlers bind to schedules by `JobKey`, and **a handler whose `ScheduledJob` row does not exist
never runs** — no error, no log, just a retention policy that silently is not applied. The
deployed database holds exactly one row, `spr-inventory`.

So all three of these were dead on arrival: the Phase 2 OAuth state purge, and both new retention
sweeps. `SeedRetentionScheduledJobs` inserts them with `ON CONFLICT ("JobKey") DO NOTHING`, so it
is idempotent, does not fight rows created through the admin portal, and preserves an operator's
retuned schedule.

Staggered at 03:15, 03:30 and 03:45 UTC — three sweeps starting together on a database shared with
Merchant360 is a self-inflicted load spike.

`RetentionJobWiringTests` asserts that each registered handler has an enabled row and that the
three schedules differ. That is the test the original A2 finding needed and did not have.

## 5. Tests

| Area | Count |
|---|---|
| Redaction (`PayloadRedactorTests`) | 15 |
| Archive, purge, buyer-data retention (`PayloadArchiveAndRetentionTests`) | 16 |
| Job wiring (`RetentionJobWiringTests`) | 4 |
| Client archiving (`AmazonOrdersClientTests`) | 3 |

Including the one that matters most — `No_fragment_of_the_original_personal_data_survives_anywhere_in_the_output`,
which searches the stored text for every personal value in the fixture and finds none of them.

**307 unit, 166 PostgreSQL, 11 integration pass.**

## 6. Capacity

The capacity assessment named this the one Phase 3 component with open-ended growth, and retention
is what closes it. Redacted order payloads run a few KB; at 30 days, dedupe on unchanged orders,
and a hard size cap, the archive is bounded by construction rather than by hoping.
