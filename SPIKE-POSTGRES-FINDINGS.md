# PostgreSQL spike — PartnerConnect findings

Target: PostgreSQL 18.4 (local). Branch: `spike/postgres`.
Companion to Merchant360's spike; same recipe, same measurements.

## Result

- Solution builds against Npgsql: **0 compile errors**
- Schema applies: **107 tables**
- `SqlBulkCopy` → Npgsql binary `COPY` ported and measured:
  **95,000 SprPriceRecords in 1,204 ms (78,904 rows/sec)**, all rows verified

## Failures encountered, in order

| # | Stage | Failure | Fix | Sites |
|---|-------|---------|-----|-------|
| 1 | compile | `Microsoft.Data` namespace missing — `SqlBulkCopy` | port to Npgsql binary `COPY` | 1 method |
| 2 | compile | `DbFunctions` has no `DateDiffMillisecond` (SQL Server-only) | plain `DateTime` subtraction → `.TotalMilliseconds` | 1 |
| 3 | schema | `type "nvarchar" does not exist` | `nvarchar(max)`→`text` | 18 |
| 4 | schema | `syntax error at or near "["` | `HasFilter`: `[X]`→`"X"` (no booleans, so no `true`/`false` conversion) | 13 |

## PartnerConnect vs Merchant360

| | Merchant360 | PartnerConnect |
|---|---|---|
| Migrations | 154 | 43 |
| DbSets | 183 | 104 |
| `UseSqlServer` sites | 5 | **1** |
| `HasFilter` | 57 | 13 |
| Computed columns | 2 | 0 |
| `HasDefaultValueSql` | 5 | 0 |
| Column types | 30 | 18 |
| Hangfire | yes | none |
| `SqlBulkCopy` | none | **2 implementations** |
| Tests | 1,233 InMemory | 167 InMemory |

PC's EF surface is roughly half M360's, and EF is confined to the `Persistence` project —
one provider call site instead of five. The bulk copy is the only thing that is genuinely
harder, and one of the two is now done.

## The bulk copy port

`SprPriceRecordRepository.BulkInsertAsync` was already generic — it reflects over EF metadata
to build a `DataTable` rather than hard-coding columns. The Npgsql version drops the
`DataTable` entirely and streams straight into `BeginBinaryImportAsync`, so it is **shorter**
than the original. The comment's original rationale (EF change detection is O(n²) and blows
past App Service's 230 s limit) holds and is better served: 95k rows in 1.2 s.

## The CSV importer port (second bulk path) — DONE

`SprCsvBulkImportService` now streams into PostgreSQL text-format `COPY` instead of
`SqlBulkCopy`. `SprCsvDataReader` is **unchanged** — its parsing is proven, and it already
hands back strings, which is exactly what text COPY wants. Every `spr.*` raw column is
`text`, so there is no type conversion at all: the reader's strings go straight in.

Measured on 500,000 rows of `productattribute` (the biggest raw table — 5.3M rows in dev):

| | |
|---|---|
| Throughput | **500,000 rows in 3,804 ms — 131,441 rows/sec** |
| Rows landed | 500,000 / 500,000 |
| Empty → NULL | 71,429 (exact) |
| Embedded commas preserved | 500 (exact) |
| Escaped quotes preserved | 334 (exact) |

Extrapolated, the full 5.3M-row `productattribute` import is roughly 40 seconds.

`BulkInsertBatchSize` no longer applies — COPY is a single continuous transfer.

**`Microsoft.Data.SqlClient` has been removed from `WorkerProcesses`. PartnerConnect now has
no SQL Server dependency anywhere, and the solution builds clean.**

## The T-SQL port (SprRawToCanonicalTransformService) — DONE

I originally described this as "31 mechanical fragments". That was wrong. Reading the file,
almost every SQL statement in it needed rewriting, and three things had no direct equivalent.
The whole transform pipeline now runs on PostgreSQL, verified end to end.

**Genuinely mechanical:** `GETUTCDATE()` → `now()` (16), `ISNULL` → `COALESCE`, `LEN` →
`length`, `NVARCHAR(n)` → `VARCHAR(n)`, string `+` → `||`.

**Positional, not substitution:** `SELECT TOP 1 … ` → `… LIMIT 1` (10 sites, all correlated
subqueries spanning several lines).

**Genuine rewrites:**
| T-SQL | PostgreSQL |
|---|---|
| `UPDATE c SET c.X FROM T c JOIN …` | target named in `UPDATE`, join moved to `WHERE`, no alias on `SET` |
| `WITH Hierarchy AS (… UNION ALL …)` | `WITH RECURSIVE` — required explicitly |
| `OUTPUT INSERTED.Id` | `RETURNING "Id" AS "Value"` (aliased for EF's `SqlQueryRaw<int>`) |
| `STRING_AGG(x, '') WITHIN GROUP (ORDER BY …)` | `string_agg(x, '' ORDER BY …)` — ordering moves inside |
| `FORMAT(GETUTCDATE(),'yyyy.MM.dd')` | `to_char(now(),'YYYY.MM.DD')` |
| `IF NOT EXISTS (… sys.indexes …) CREATE NONCLUSTERED INDEX` | `CREATE INDEX IF NOT EXISTS` — the check is deleted, not translated |
| `TRY_CAST(x AS INT)` | **no equivalent** — regex-guarded `CASE` at 3 sites |

**Plus the thing no grep would have found:** every EF-created table and column had to be
quoted. Npgsql folds unquoted identifiers to lower case, so `SprProductContent` becomes
`sprproductcontent` and does not exist. The `spr.*` raw tables are already lower case and
were fine; every canonical one was not.

Also removed a 27-line dead SQL block (`CS0219`, pre-existing on `main`).

### Verified end to end

Seeded the `spr.*` raw tables and ran the whole pipeline:

```
TransformCategoriesAsync     -> 3      FullPath "10/11/12"  (recursive CTE + UPDATE..FROM)
TransformProductsAsync       -> 2      Sku SPR1001, fallback MPN-2  (10x TOP 1 -> LIMIT 1)
TransformFeaturesAsync       -> 2      non-numeric sequenceno fell back to ROW_NUMBER
TransformRelationshipsAsync  -> 3      1 bidirectional (boolean conversion correct)
TransformSpecificationsAsync -> 1      HTML ordered + escaped identically
```

Spec HTML output, with the `'oops'` display-order sorting last exactly as `TRY_CAST` made it:
`<table class="specs"><tr><th>Colour</th>…<tr><th>Weight</th>…</table>`

### The subtle one: a lost column default

`TransformProductsAsync` failed at first with a NOT NULL violation on
`M360PushTotalProducts`. Not a translation bug — those four columns carried `DEFAULT ((0))`
on SQL Server *only* because migration `20260709195214_AddM360ContentPushStatus` passed
`defaultValue: 0` to `AddColumn`. That is a backfill value for existing rows, **not part of
the model**, so rebaselining regenerated the schema without it. Fixed by declaring the
defaults in `SprContentUploadConfiguration` so they survive future rebaselines.

This is the category of problem that only running the code finds: the schema applied
cleanly, the code compiled, and the defect was a missing default that production SQL
silently depended on.

## Runtime verification — API, workers, tests, collation

**Test suite:** 175 tests, **174 pass, 1 fails**. The failure
(`SprFlowSmokeTests.Flow1_Outbound`) fails identically on `main` at `6e6acd1` — verified in a
throwaway worktree. Pre-existing, not caused by the port.

**API:** starts on PostgreSQL, `/health` 200, Swagger generates (265 paths). All 75
parameterless GETs probed: 72 x 401, 2 x 400, 1 x 200, **zero 500s**. The 401s are the global
API-key middleware (`WWW-Authenticate: ApiKey`), so HTTP probing cannot reach the data layer -
which is why the repository probes below matter more.

**Workers:** all 8 start and poll PostgreSQL cleanly - Price Feed Sync, Price Feed Upload
Processing, Document Processing, Content Sync, Outbox, EDI Document Sync, SPR Content
Ingestion, FTP Ingestion Queue. Generated SQL is correctly quoted, including the `M360Push*`
columns whose defaults were restored above. **Zero PostgresExceptions.**

But getting there needed a real fix. The workers initially died with
`FileNotFoundException: Microsoft.EntityFrameworkCore.Relational, Version=8.0.29.0`. Cause:
`Microsoft.EntityFrameworkCore.InMemory 8.0.29` is pinned in Persistence, and the old
`Microsoft.EntityFrameworkCore.SqlServer 8.0.29` used to hold every EF assembly at that
version. `Npgsql.EntityFrameworkCore.PostgreSQL 8.0.*` resolves EF **8.0.11**, so the solution
compiled against 8.0.29 references and shipped 8.0.11. The API happened to work; the workers
did not. Fixed by pinning `Microsoft.EntityFrameworkCore.Relational` to 8.0.29.

**DateDiffMillisecond, take two.** The earlier "port" - plain `DateTime` subtraction plus
`.TotalMilliseconds` - compiles but **Npgsql cannot translate it**, so `GetStatisticsAsync`
threw at runtime. PostgreSQL needs `EXTRACT(EPOCH FROM interval)`, which has no LINQ
equivalent, so it now goes through a raw scalar query. Verified: a message seeded 5 minutes
before delivery reports `avg delivery 300,000 ms`.

**DateTime.Kind:** `DateTime.UtcNow` predicates work; `new DateTime(...)` and `DateTime.Today`
throw, exactly as on Merchant360. PartnerConnect's exposure is far smaller though - **0
`DateTime.Today`, 0 `DateTime.Now`**, 508 `DateTime.UtcNow`, 10 `new DateTime(`.

**Case sensitivity — demonstrated, then fixed.** Before:

| | PostgreSQL | SQL Server |
|---|---|---|
| `Username == "PCAdmin"` (row is `pcadmin`) | 0 matches | 1 match |
| insert `PCADMIN` next to `pcadmin` | **INSERTED** | rejected by unique index |

That second row is the dangerous one: a duplicate admin account the unique index was supposed
to prevent. Fixed by declaring a non-deterministic ICU collation on the model
(`und-u-ks-level2` - case-insensitive, accent-sensitive, matching
`SQL_Latin1_General_CP1_CI_AS`) and applying it to `AdminPortalUser.Username`. After:
wrong-case lookup matches, `LIKE '%ADMIN%'` matches (PG 18 support confirmed on a real
column), and the duplicate insert is rejected by `IX_AdminPortalUsers_Username`.

Only `Username` is converted so far; the other identity columns (`Organizations.Code`,
`TradingPartners.Code`, emails, `SprPriceRecords.StockNumber`) still need the same treatment.

## Incidental pre-existing bugs found

- `IFeedProcessingService` has **zero DI registrations**, yet `ContentSyncWorker`,
  `InventoryFeedSyncWorker` and `PriceFeedSyncWorker` all call
  `GetRequiredService<IFeedProcessingService>()`. Those workers throw on every poll, on `main`
  too.
- `SprFlowSmokeTests.Flow1_Outbound` red on `main`.
- 27-line dead SQL block in `SprRawToCanonicalTransformService` (CS0219).

## Still outstanding
3. Not exercised: running the API/workers, the 167 InMemory tests, case-insensitivity,
   `DateTime.Kind` (M360 needed the legacy switch; PC untested).

## Cross-system price push - verified end to end on PostgreSQL

Both systems running on PostgreSQL, PartnerConnect pushing a real price feed into
Merchant360 through the production path: OAuth2 client-credentials token, the batch
endpoint, EFCore.BulkExtensions ingest, and the `JoinKey` generated column.

Sequence exercised:

1. PartnerConnect obtains a token from Merchant360's `/oauth2/token`
   (`partnerconnect-service`, scope `merchant360.prices.write`).
2. A `PriceFeedUpload` in `Completed` moves to `PushQueued`.
3. `PriceFeedUploadProcessingWorker` claims it and calls `ProcessQueuedPushAsync`.
4. `Merchant360ApiClient` POSTs to
   `/api/v1/partner-connect/merchants/{merchantId}/prices/batch`.
5. Merchant360 ingests into `PC_MerchantPrices`; the upload becomes
   `PushedToMerchant360`.

Verified in Merchant360's database afterwards:

| Check | Result |
|---|---|
| Rows arrived | 2 of 2 |
| `merchantId` resolved from PC `Tenant.ExternalId` | 3 (not PartnerConnect's DealerId) |
| `JoinKey` computed | `XSYS-100-A` -> `XSYS100A`, `XSYS-200` -> `XSYS200` |
| Effective cost | promo 19.75 beat reference 25.00; promo 0 fell back to 60.00 |
| Sync log | Prices / Completed / 2 received / 2 created |

Separately, pushing price and content batches that key on *different* stock numbers -
priced as `HAM10501-5`, described as `HAM105015` - confirmed the two feeds still join
through `JoinKey`, which is the reason that column exists.

### Finding: the trading-partner id is an implicit contract, not a mapping

The first push failed with `Trading partner 1 not found or inactive`.

PartnerConnect sends its own `upload.TradingPartnerId`. Merchant360 matches on
`tp.Id == request.TradingPartnerId` - its own primary key. So the contract silently
requires the two systems to have assigned the *same integer* to the same partner.

Merchant360's `TradingPartners.PartnerConnectId` column exists to hold PartnerConnect's
id, and this lookup does not use it.

It works today only because both databases happen to have SPR at id 1. It surfaced here
because a fresh Merchant360 database assigned id 2. Nothing to do with PostgreSQL - the
same mismatch would break the push on SQL Server the moment a new environment assigns
ids in a different order, which is exactly what happens when standing up preprod or
production.

Worth resolving deliberately: either match on `PartnerConnectId`, or state the shared-id
requirement in the contract and enforce it at onboarding.
