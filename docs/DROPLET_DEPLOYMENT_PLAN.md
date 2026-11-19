# Droplet deployment plan — 27 pending commits, 10 pending migrations

Written 2026-11-18 for review. **Nothing in this plan has been executed.** It describes a
service-changing deployment, which the standing constraint reserves for separate explicit
approval.

Target: the shared test droplet `164.90.153.14` (DigitalOcean), applications `pc-api`,
`pc-adminportal`, `pc-customerportal`, `pc-workers`. Merchant360's four applications on the same
droplet are **not** touched.

---

## 1. What is actually pending

Measured, not assumed:

| | Deployed now | Repository HEAD |
|---|---|---|
| `pc-api` binary | `2026-11-02 04:01` → commit `871af8a` | `97debdc` |
| `pc-workers` binary | **`2026-10-27 20:04`** — three weeks staler than the API | `97debdc` |
| `partnerconnect` schema | `20261027155614_AddM360PushRecordCounts` | `20261118224101_AddPriceFeedUploadAccount` |

27 commits (`871af8a..97debdc`) and 10 migrations.

**The workers app is the larger jump.** Its binary predates the API's by six days, so deploying
it picks up changes that were never shipped in the 2026-11-02 round as well as everything since.
This is also the app that has been logging `IFeedProcessingService has not been registered` every
five minutes; commit `23733cb` is the fix and it is in this payload.

### The 10 migrations, in order

All ten are **additive** — new tables, new nullable column, new index, seed inserts. None drops or
rewrites an existing column, which is what makes "schema first, binaries second" the safe order.

| # | Migration | Effect |
|---|---|---|
| 1 | `AddIntegrationSubstrate` | new `IntegrationProviders`, `IntegrationConnections`, `IntegrationCredentials`, `IntegrationSyncStates` |
| 2 | `AddIntegrationOAuthStateAndProviderSeed` | new `IntegrationOAuthStates`; seeds `AMAZON_SP` and `AMAZON_BUSINESS` providers (`AMAZON_BUSINESS` explicitly `IsEnabled = false`) |
| 3 | `AddSalesOrders` | new `SalesOrders`, `SalesOrderLines`, `SalesOrderStatusHistory` |
| 4 | `AddProviderRateLimitBuckets` | new `ProviderRateLimitBuckets` |
| 5 | `AddSellerAccountOwnershipAndDelegation` | `IntegrationConnections.AccountOwnership` + new `IntegrationAccountDelegations` + the `UX_IntegrationConnections_PrimaryAccountOwner` filtered unique index |
| 6 | `AddIntegrationPayloadArchive` | new `IntegrationPayloadArchives` |
| 7 | `SeedRetentionScheduledJobs` | **three new `ScheduledJobs` rows, `IsEnabled = true`** (03:15 / 03:30 / 03:45 UTC) |
| 8 | `SeedReconciliationScheduledJob` | **one new `ScheduledJobs` row, `IsEnabled = true`** (04:15 UTC) |
| 9 | `AddIntegrationEvents` | new `IntegrationEvents` |
| 10 | `AddPriceFeedUploadAccount` | `PriceFeedUploads.TenantPartnerAccountId` (nullable), filtered index, FK `ON DELETE RESTRICT` |

Old binaries tolerate all ten: EF emits explicit column lists, so the `pc-api` build currently
running will not see `TenantPartnerAccountId`, and it has no interest in the new tables. That is
the property that lets the schema go first and sit there safely if the binary step is aborted.

### What changes behaviour on the droplet, and what does not

**Dormant on arrival — no live effect:**

- All Amazon order ingestion. `Workers:SalesOrderPolling:Enabled` is `false` in
  `appsettings.json` and is **not** overridden in `/etc/merchant360/env/pc-workers.env`, so
  `SalesOrderPollingWorker` logs `Sales-order polling is disabled.` and returns. Confirmed in
  `SalesOrderPollingWorker.cs:39`.
- The Amazon consent flow. No `AmazonLwa:*` configuration exists, `AmazonLwaClient.IsConfigured`
  is false, and there is no `ValidateOnStart` anywhere in the solution — so the absent
  configuration binds empty and throws nothing at startup.
- The four seeded scheduled jobs. They are enabled and their handlers are registered
  (`ServiceCollectionExtensions.cs:95,96,110,124`), so they **will** fire on the first night. Each
  one sweeps a table this migration has just created empty: OAuth states, payload archives, sales
  orders. Reconciliation iterates integration connections, of which there are none. All four are
  no-ops that will log a zero count. `ScheduledJobs` today holds exactly one row, `spr-inventory`,
  which is untouched.
- The colon-account column. `PriceFeedUploads.TenantPartnerAccountId` is nullable and nothing
  writes it; the 4 existing upload rows stay `NULL` and every read path still resolves price per
  tenant, not per account. The multiple-colon-account feature is **not** implemented — this
  migration only adds the column it will need.

**Changes behaviour:**

- `pc-workers` stops logging the `IFeedProcessingService` registration error every five minutes
  and starts actually resolving the feed services.
- Price-feed sync stops recording a batch row for a poll that found no file (`RecordsNoFile`).
- SPR price-file parsing resolves dealer cost from the contract column first
  (`SprDealerCost.Resolve`). This affects the SFTP parser path only, which has never successfully
  run — see §6.
- Admin portal gains the Marketplace Orders page; it will render an empty grid.

---

## 2. Preconditions

| Check | State |
|---|---|
| `CredentialEncryption__EncryptionKey` set in `pc-api.env` and `pc-workers.env` | **Done** 2026-11-19 04:10 UTC, identical in both, 44 base64 chars, `0600`, hash `b90061e3b710e09d`. This was the sole blocker: `AesCredentialProtector` fails closed outside Development (`AesCredentialProtector.cs:40`) and both units run `ASPNETCORE_ENVIRONMENT=Production`. **No restart has been performed, so it has not yet been exercised.** |
| Key recorded in the password manager | **To confirm by the operator before starting.** If the key is lost after a restart, any value written through `ICredentialProtector` becomes unreadable. Nothing is encrypted with it today (`PortalApiKey` is NULL, SPR credentials are plaintext), so loss is recoverable now and will not be later. |
| The portals do not need the key | `pc-adminportal` / `pc-customerportal` do not resolve `ICredentialProtector`; they were never blocked. |
| .NET 8 runtime on the droplet | `8.0.34` (ASP.NET Core + NETCore). `deploy.sh` publishes framework-dependent, so this must stay true. |
| Disk | 208 GB free of 232 GB. |
| Memory | 4.5 GB available of 7.9 GB, and the earlier "swap in use" reading was not memory pressure — `vmstat` si/so were 0 across samples. |
| Nightly backup | `/root/droplet/backup.sh` via timer at 02:30 UTC; latest set `20261119-023029` holds `partnerconnect.dump` (206 MB), `merchant360.dump`, `env.tar.gz`, `uploads.tar.gz`, `config.tar.gz`. 14-day retention. |
| No droplet-side config to preserve | Neither `pc-api` nor `pc-workers` has an `appsettings.Production.json` on the droplet, so `deploy.sh`'s exclusion of that file is moot and all configuration comes from the shipped `appsettings.json` plus the env file. |

---

## 3. The deployment, step by step

Run from the Mac. `deploy.sh` lives in the Merchant360 repo and finds this repo as a sibling.

### Step 0 — fresh dump, immediately before touching anything

The nightly set is up to 24 hours old. Take a point-in-time dump so rollback does not lose a
day:

```bash
ssh root@164.90.153.14 \
  'sudo -u postgres pg_dump -Fc -d partnerconnect \
     -f /var/backups/merchant360/pre-deploy-$(date -u +%Y%m%d-%H%M%S).dump && \
   ls -la /var/backups/merchant360/pre-deploy-*.dump'
```

This writes a new file; it does not modify or delete any retained set.

### Step 1 — preserve the current binaries

`deploy.sh` uses `rsync -az --delete`, which **destroys the directory it replaces**. The only way
back to the exact binaries running today — particularly `pc-workers`, whose source commit is not
recorded anywhere — is to copy them aside first:

```bash
ssh root@164.90.153.14 \
  'cd /opt/merchant360 && tar czf /root/pc-rollback-$(date -u +%Y%m%d-%H%M%S).tar.gz \
     pc-api pc-workers pc-adminportal pc-customerportal && \
   ls -la /root/pc-rollback-*.tar.gz'
```

Without this, binary rollback means rebuilding from `871af8a`, which for `pc-workers` would ship
a build that has never run here.

### Step 2 — apply the migrations

`deploy.sh` deliberately does not run migrations ("a failed migration mid-deploy is worse than a
manual step"). Do them first and on their own, while the current binaries are still serving:

```bash
# terminal 1 — tunnel, PostgreSQL listens on loopback only
ssh -L 15432:127.0.0.1:5432 root@164.90.153.14

# terminal 2 — dry run first: print the SQL and read it
cd ~/dev/Vendorea.PartnerConnect/src/Vendorea.PartnerConnect.Persistence
dotnet ef migrations script 20261027155614_AddM360PushRecordCounts \
  --startup-project ../Vendorea.PartnerConnect.API \
  --output /tmp/pending.sql
less /tmp/pending.sql

# then apply
dotnet ef database update \
  --startup-project ../Vendorea.PartnerConnect.API \
  --connection 'Host=127.0.0.1;Port=15432;Database=partnerconnect;Username=partnerconnect;Password=<from pc-api.env>'
```

Expect 10 migrations applied and no error. If any fails, **stop** — the binaries are untouched and
the system is still serving; restore from the Step 0 dump only if the failure left the schema
partially applied (EF wraps each migration in a transaction, so a single failure normally leaves
the preceding ones applied and the failing one rolled back).

### Step 3 — verify the schema before the binaries

```bash
ssh root@164.90.153.14 'sudo -u postgres psql -d partnerconnect \
  -c "SELECT \"MigrationId\" FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\" DESC LIMIT 3;" \
  -c "SELECT \"JobKey\", \"IsEnabled\", \"CronExpression\" FROM \"ScheduledJobs\" ORDER BY \"JobKey\";" \
  -c "SELECT \"Code\", \"IsEnabled\" FROM \"IntegrationProviders\";" \
  -c "SELECT count(*) FROM \"PriceFeedUploads\" WHERE \"TenantPartnerAccountId\" IS NOT NULL;"'
```

Expect: `AddPriceFeedUploadAccount` newest; five `ScheduledJobs` rows with `spr-inventory` still
enabled and `AMAZON_BUSINESS` **`IsEnabled = false`**; zero populated account ids.

`AMAZON_BUSINESS.IsEnabled` is called out because the generated migration originally omitted the
column, which would have taken the CLR default `false`… against a column default of `true`, and
enabled it everywhere. The migration was hand-edited to list the column. This check confirms the
edit survived.

The old binaries are still running at this point and must still be healthy:

```bash
ssh root@164.90.153.14 '/root/droplet/health.sh'
```

### Step 4 — ship the binaries

```bash
cd ~/dev/MErchant360Claude
./deploy/droplet/deploy.sh 164.90.153.14 pc-api pc-workers pc-adminportal pc-customerportal
```

What it does, in its own order: `dotnet publish` each project locally → `systemctl stop` all four
(before copying, so rsync never overwrites a DLL under a running process) → `rsync -az --delete`
→ `chown -R merchant360:merchant360` → print the migration reminder (already done, ignore it) →
`systemctl start` all four → `sleep 8` → print `is-active`.

All four are down together for the duration of the rsync. The droplet is a test environment with
no live traffic, so this is acceptable; it is not a rolling deploy.

### Step 5 — verify the new binaries

```bash
ssh root@164.90.153.14 'systemctl is-active pc-api pc-workers pc-adminportal pc-customerportal'
ssh root@164.90.153.14 '/root/droplet/health.sh'
ssh root@164.90.153.14 'curl -s -o /dev/null -w "pc-api health: %{http_code}\n" http://127.0.0.1:5010/health'
```

Then read the logs, which is where this deployment can actually fail:

```bash
# the encryption key, exercised for the first time. A startup crash here is
# "CredentialEncryption:EncryptionKey is not configured" and means the env file is not
# being read by the unit, not that the key is wrong.
ssh root@164.90.153.14 'journalctl -u pc-api -u pc-workers --since "10 min ago" -p warning --no-pager | tail -40'

# must be present, and must say disabled
ssh root@164.90.153.14 'journalctl -u pc-workers --since "10 min ago" --no-pager | grep -i "sales-order polling"'

# must be gone
ssh root@164.90.153.14 'journalctl -u pc-workers --since "10 min ago" --no-pager | grep -c "IFeedProcessingService"'
```

Expect: all four `active`; `health.sh` green; `Sales-order polling is disabled.`; zero
`IFeedProcessingService` lines.

### Step 6 — the morning after

The four new jobs fire at 03:15, 03:30, 03:45 and 04:15 UTC. Confirm they ran and did nothing:

```bash
ssh root@164.90.153.14 'sudo -u postgres psql -d partnerconnect -c "
  SELECT \"JobKey\", \"LastRunAt\", \"LastRunStatus\", \"LastRunDetail\"
  FROM \"ScheduledJobs\" ORDER BY \"JobKey\";"'
```

---

## 4. Rollback

In the order you would reach for them.

**Binaries only** (the likely case: a service will not start):

```bash
ssh root@164.90.153.14 'systemctl stop pc-api pc-workers pc-adminportal pc-customerportal && \
  cd /opt/merchant360 && rm -rf pc-api pc-workers pc-adminportal pc-customerportal && \
  tar xzf /root/pc-rollback-<timestamp>.tar.gz && \
  chown -R merchant360:merchant360 /opt/merchant360 && \
  systemctl start pc-api pc-workers pc-adminportal pc-customerportal'
```

The schema can stay ahead. Every migration is additive and the old binaries ignore all of it —
that is the whole reason for the migrations-first order. **Do not run `database update` backwards
to recover from a binary problem.**

**Schema** (only if a migration itself is the problem, before binaries are shipped):

```bash
cd ~/dev/Vendorea.PartnerConnect/src/Vendorea.PartnerConnect.Persistence
dotnet ef database update 20261027155614_AddM360PushRecordCounts \
  --startup-project ../Vendorea.PartnerConnect.API --connection '...'
```

The `Down` methods drop the new tables and the new column and delete the four seeded job rows.
They touch nothing that existed before, so this is reversible — but it discards any row written to
the new tables in between, which after Step 4 is no longer nothing.

**Full restore** (last resort):

```bash
ssh root@164.90.153.14 'sudo -u postgres pg_restore -d partnerconnect --clean --if-exists \
  /var/backups/merchant360/pre-deploy-<timestamp>.dump'
```

Destroys and recreates the database contents. Requires the services stopped.

**The encryption key** is not part of any rollback. Removing it re-breaks startup for any build
after Phase 1. Backups of both env files are at `/root/env-backup-pc-api.env.20261119-041027` and
`/root/env-backup-pc-workers.env.20261119-041027`.

---

## 5. Risks, stated plainly

| Risk | Likelihood | Mitigation |
|---|---|---|
| `pc-api` or `pc-workers` fails to start because the env file is not reaching the process | Low — the key is present, identical, `0600`, and the unit has `EnvironmentFile` | This is the first restart since the key was set, so it is unproven. Step 5 reads the logs for exactly this. Step 1 makes it reversible in a minute. |
| `pc-workers` regresses in some unrelated way, being a three-week-older binary than `pc-api` | Moderate — the largest untested delta in this payload | Step 1 tarball. Note that the workers app is already partly broken today (`IFeedProcessingService`), so "as it was" is not a healthy baseline. |
| A seeded job errors instead of no-opping | Low | Step 6 reads `LastRunStatus`. A failing job logs and retries on its schedule; it does not take the process down. |
| Amazon code paths somehow activate | Very low | Polling is off by configuration; there are no connections, no credentials and no LWA configuration. Three independent reasons, any one sufficient. |
| Migration 10's `ON DELETE RESTRICT` blocks deleting a `TenantPartnerAccount` | Low | Only once an upload references an account, which nothing does yet. The behaviour is intentional and proven by test (PostgreSQL raises `23001 restrict_violation`, not `23503`). |
| Both databases share one PostgreSQL instance with Merchant360 | Standing | 12 of 120 connections in use. The new nightly sweeps were given 03:15–04:15 UTC specifically to avoid piling onto the existing 02:30 backup and 06:00 SPR inventory job. |

---

## 6. What this deployment does *not* fix

Stated so the post-deploy state is not mistaken for a clean one.

- **The SPR SFTP connection is still broken.** `gist.sprich.com:22` times out; port `50022`
  answers as "SFTP Server". The fix is `"SftpPort": 50022` in
  `TradingPartners.TransportConfigJson` (and arguably the code default), and it is **not** in this
  payload. Until it lands, `PriceFeedSyncWorker` keeps failing every cycle.
- **The 22,338 empty `PriceFeedBatch` rows keep accumulating.** They are `Status = Failed` with
  `Connection failed to establish within 30000 milliseconds`, not no-op polls. The `RecordsNoFile`
  guard in this payload requires `Completed`, so it correctly does not suppress them — they are
  the record of an integration that has been down for 86 days. The earlier proposed `DELETE`
  matched 0 rows, which is the right outcome; a `DELETE` written on `TotalItems = 0` alone would
  have destroyed that evidence.
- **The SFTP feed path still discards what it parses.** It counts `parseResult.Items.Count` and
  persists nothing, and `FeedProcessingService` fabricates the counts it reports. Fixing the port
  without fixing this yields successful batches that store no prices.
- **Colon accounts are not implemented.** Only the column exists.
- **Backup encryption remains deferred to production**, per the recorded decision — along with
  its real test-environment counterpart, asking SPR for test-only SFTP credentials so the
  droplet's backups stop containing a live supplier's plaintext passwords.
- **Amazon is not connectable.** Developer registration (application id, client id/secret,
  redirect URI, approved roles) is still outstanding, and the Restricted Data Token needed for
  buyer name and shipping address is backlog item 3.13.

---

## 7. Authorization

This plan performs a service-changing deployment: it stops and restarts four systemd units and
alters a shared production-mode database schema. Under the standing constraint — *"do not perform
live credential rotation, backup deletion, or service-changing deployment without explicit
operational approval"* — none of §3 runs until that approval is given.

It does **not** rotate any credential, delete or modify any retained backup, or replace the backup
process.
