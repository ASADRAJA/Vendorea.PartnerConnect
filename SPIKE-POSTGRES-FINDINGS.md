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

## Still outstanding

1. **Raw SQL T-SQL** across 3 files: 16 × `GETUTCDATE()`, 10 × `TOP 1`, 3 × `ISNULL(`,
   2 × `sys.indexes`. All mechanical.
3. Not exercised: running the API/workers, the 167 InMemory tests, case-insensitivity,
   `DateTime.Kind` (M360 needed the legacy switch; PC untested).
