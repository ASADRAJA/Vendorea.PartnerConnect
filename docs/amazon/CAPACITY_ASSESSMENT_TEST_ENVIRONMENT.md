# Capacity Assessment — Test Environment (droplet 164.90.153.14)

**Date:** 2026-11-06
**Scope:** test environment only. Production will be a separate, more performant system.
**Stated load:** no more than 10 concurrent users, ever.

## Verdict: **no upgrade needed.** Activate Phase 3 here as it stands.

---

## 1. Correcting my earlier framing

The capacity gate in the backlog reads *"the droplet shares PostgreSQL with M360 on 8 GB with
swap in use."* That was reasoning from a static number. Measured:

```
Swap:  530 MB used  —  but vmstat si/so = 0, 0 over three samples
```

530 MB sits in swap from somewhere in 80 days of uptime and has not been touched since. **Nothing
is swapping.** "Swap in use" and "under memory pressure" are different conditions, and I had
treated the first as evidence of the second.

## 2. What is actually there

| Resource | Measured | Headroom |
|---|---|---|
| RAM | 7.9 GB total, 3.1 GB used, **4.8 GB available** | ample |
| Swap activity | si/so = 0 | not swapping |
| CPU | 4 cores, load average **0.17** | essentially idle |
| Disk | 26 GB used of 232 GB — **207 GB free** | ample |
| PostgreSQL connections | **12 of 120** in use (5 M360, 5 PC, 2 other) | ample |
| `shared_buffers` | 1.25 GB | sized sensibly for 8 GB |

A note on reading `ps`: the postgres `checkpointer` and `background writer` show 1057 MB and
850 MB RSS. That is overwhelmingly the 1.25 GB shared-memory segment mapped into each process and
counted again per process. It is not additive, and the real figure is the 3.1 GB `free -m` reports.

### Where the 3.2 GB of `partnerconnect` went

| Table | Size |
|---|---|
| `SprProductRelationships` | 342 MB |
| `SprProductContent` | 225 MB |
| `SprProductFeatures` | 155 MB |
| `SupplierInventoryLocationQuantities` | 137 MB |
| `SprProductSpecifications` | 107 MB |
| `SprPriceRecords` | 91 MB |

Static supplier catalogue content, not transactional growth. It is the largest thing in the
database and it does not grow with usage.

## 3. What Phase 3 adds

### Storage

Per order, all-in — row, ~2 lines, 2–4 history entries, and the six indexes:

| Component | Bytes |
|---|---|
| `SalesOrders` row + indexes | ~1,500 |
| `SalesOrderLines` (×2) | ~1,600 |
| `SalesOrderStatusHistory` (×3) | ~500 |
| jsonb (`programsRaw`, `proceedsRaw`) | ~400 |
| **Total** | **~4 KB; budget 5 KB** |

| Scenario | Orders | Storage |
|---|---|---|
| Realistic test use | 1,000 | **5 MB** |
| Heavy test use | 10,000 | **50 MB** |
| One connection at the **theoretical provider ceiling** for 30 days | 1.44 M | 7.2 GB |

Against 207 GB free, even the third line fits — and it cannot occur here, because it requires a
real seller with 1.4 million orders. Realistic test use is **0.002%** of free disk.

### The rate limit is the capacity governor

`searchOrders` is metered at 0.0056 req/s — one call per ~179 seconds, 20 pages/hour per seller
account. At 100 orders per page that is a hard ceiling of **2,000 orders/hour per connection**,
imposed by Amazon, before any of our configuration applies. Ingestion cannot spike. Whatever else
is true of this droplet, it will not be surprised by a burst of marketplace orders.

### Memory and connections

- The polling worker runs at most 4 connections concurrently, each in its own scope. A page of
  100 orders tracks roughly 400 entities — tens of MB, transient. The workers process sits at
  150 MB RSS today; expect peaks of 250–350 MB. Against 4.8 GB available.
- Up to 4 additional PostgreSQL connections while polling. 12 of 120 are in use.

### CPU

Parsing 100 orders of JSON every three minutes, on 4 cores at 0.17 load.

## 4. Why 10 concurrent users was never the binding constraint

The concern was never interactive load — it was unbounded background ingestion sharing a database
with Merchant360. Two things resolve it, and neither depends on user count:

1. Amazon's own rate limit caps ingestion at 2,000 orders/hour per connection.
2. The walk is bounded at `MaxPagesPerRun` and persists its cursor, so a long backfill is spread
   across runs rather than held inside one.

## 5. Settings for this environment

No upgrade, but three deliberate choices:

| Setting | Value | Why |
|---|---|---|
| `Workers:SalesOrderPolling:IntervalMinutes` | 15 | 5 is the floor the provider imposes; 15 is comfortable for a test environment |
| `Workers:SalesOrderPolling:MaxConcurrency` | 4 (leave) | Harmless with 2 tenants — the semaphore simply never saturates |
| `Amazon:OrderPolling:InitialBackfill` | **7 days** for test, not the 30-day default | A test seller with real history would otherwise walk for hours at one page per three minutes. Shorten it here; production decides on its own merits |

### Two operational cautions

- **Do not run a first Amazon backfill while an SPR content import is in flight.** The content
  import is the memory-heavy job on this host — almost certainly what put those 530 MB into swap
  — and it is the one thing that could make the two interact.
- **Bound `RawDocumentArchive` retention before enabling it** (finding A2, item 3.10). Raw order
  payloads with `includedData` run 4–8 KB each and would be stored *in addition* to the parsed
  rows. That is the one Phase 3 component with genuinely open-ended growth, and retention is what
  closes it — which is already the plan, and now also a capacity reason rather than only a privacy
  one.

## 6. Gate outcome

**The Phase 3 capacity gate is satisfied for the test environment.** It carries forward unchanged
to production, which is a separate sizing exercise against real order volumes rather than an
extrapolation from this one.
