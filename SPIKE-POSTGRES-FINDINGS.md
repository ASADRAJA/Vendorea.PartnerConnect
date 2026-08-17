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

## Still outstanding
3. Not exercised: running the API/workers, the 167 InMemory tests, case-insensitivity,
   `DateTime.Kind` (M360 needed the legacy switch; PC untested).
