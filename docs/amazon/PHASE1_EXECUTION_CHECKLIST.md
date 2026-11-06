# Phase 1 execution checklist

**Date:** 2026-11-06 · Run top to bottom. Each step states its evidence.
Nothing below has been executed.

**Rule:** steps marked **⛔ APPROVAL** need explicit operational approval before running.
Everything else is development work in the repository and needs none.

---

## Stage A — Backup separation (before any key is installed)

Why first: installing the key into the current backup régime copies it beside the data
every night. Each night of delay adds another retained set holding both halves.

| # | Step | Evidence of success |
|---|---|---|
| A1 | Decide `age` or `gpg` | Recorded decision |
| A2 | Generate the recipient keypair off-host; private key → password manager, custodian named | Custodian confirms they can retrieve it |
| A3 | Install the **public** key at `/etc/merchant360/backup-recipient.pub` | File present, 0644 |
| A4 | Confirm the tool is installed on the droplet | `age --version` succeeds |
| A5 | ⛔ **APPROVAL** — patch `backup.sh` to encrypt the env tarball | Diff reviewed |
| A6 | Tighten permissions: sets 0700, dumps 0600 | `stat` confirms |
| A7 | Run a backup | New set written, no `INCOMPLETE` |
| A8 | Verify the env tarball cannot be opened on the host | `tar -tzf` fails; `age -d` with the private key succeeds |
| A9 | **Rehearse a full restore** into a scratch database | Row counts match; env decrypts separately |

**Stop if A8 or A9 fails.** An unopenable backup and an unprotected backup are both failures.

## Stage B — Encryption key (R0.4)

| # | Step | Evidence |
|---|---|---|
| B1 | **Re-confirm the protected-field inventory** against live data | `PortalApiKey` NULL; `TransportCredentialsJson` not `enc:v1:` — if either has changed, **stop** and revise the plan |
| B2 | Announce the freeze on partner-transport and org-portal-key edits | Communicated |
| B3 | `backup.sh` run and verified | Size normal, no `INCOMPLETE` |
| B4 | Back up both env files with timestamped copies | `.bak.<ts>` present |
| B5 | Generate a unique 32-byte key off-host | `openssl rand -base64 32` |
| B6 | Store in the password manager **before** deploying | Retrievable by the custodian |
| B7 | ⛔ **APPROVAL** — append the key to `pc-api.env` and `pc-workers.env` | Both files, identical value, 0600 |
| B8 | Restart `pc-api`, wait for health | `/health` 200 |
| B9 | Restart `pc-workers`, wait for health | `/health` 200 |
| B10 | Confirm the fallback warning is gone from both | No match for "development fallback" |
| B11 | Round-trip across processes | Encrypt in API, decrypt in workers |
| B12 | **SPR smoke test** | SFTP poll succeeds; no credential error |
| B13 | **M360 smoke test** | `/api/integrations/orders` responds; four wire contracts byte-identical |

**Rollback:** restore the `.bak` env files, restart. Safe — no ciphertext was written.

## Stage C — Code remediation (repository work, no approval needed)

| # | Step | Evidence |
|---|---|---|
| C1 | Fail-closed startup outside Development | Scratch env with no key refuses to start |
| C2 | Strict `Unprotect` for the **new credential store only** | SPR legacy passthrough untouched — matrix R0.8 |
| C3 | Corrected HMAC signer as shared code | Reproduces test vector `a050819d…eedd0c` |
| C4 | Outbox enqueue overload joining a caller transaction | Matrix R0.16–R0.18 green on real PostgreSQL |
| C5 | Existing `EnqueueAsync` unchanged | SPR outbox path untouched |

## Stage D — Phase 1 build

Detailed in `PHASE1_IMPLEMENTATION_INVENTORY.md`. Order:

| # | Group | Done when |
|---|---|---|
| D1 | Entities (5) | Compile; no existing entity modified |
| D2 | EF configs + partial unique index | Matrix 1.1, 1.2 green on real PostgreSQL |
| D3 | `AddIntegrationSubstrate` migration | Applies to a copy of the production schema; **no existing table altered** |
| D4 | Repositories | Credentials absent from list projections — matrix 1.3 |
| D5 | Services | Context factory builds per call; matrix 1.8 |
| D6 | `IntegrationsController` + scopes + DTOs | Matches the v1.1 OpenAPI; matrix 1.4–1.7 |
| D7 | Audit, metrics, admin health view | Connect/disconnect audited without secrets |
| D8 | Tests | All 10 Phase 1 matrix items green |

## Stage E — Phase 1 gate

| | Check |
|---|---|
| ☐ | Two connections for one tenant, isolated in data and credentials |
| ☐ | A second tenant receives **404** for both |
| ☐ | Viewer cannot write; OrgAdmin and TenantManager can |
| ☐ | Credentials survive restart and decrypt in the other service |
| ☐ | Migration altered no existing table |
| ☐ | Atomic commit and rollback proven on real PostgreSQL |
| ☐ | Signature reproduces the shared test vector |
| ☐ | `validate_contracts.py` green in CI |
| ☐ | **175 existing unit tests green** |
| ☐ | **SPR EDI 850/855/856/810 round trip unchanged** |
| ☐ | **Four M360 message types byte-identical** |
| ☐ | No fallback-key warning in any log |

## Stage F — Deferred, explicitly

| Item | Deferred to | Why |
|---|---|---|
| SPR credential encryption (R0.9) | After B verified | Separate approval, per Decision 1; only data mutation |
| SPR password rotation (R0.10) | SPR's schedule | External; closes the historical-backup exposure |
| `RawDocumentArchive` remediation (A2) | Phase 3 | No buyer data exists until ingestion |
| Scheduler fairness (A4) | Phase 3 | No per-connection polling yet |
| OAuth endpoints | Phase 2 | — |
| Anything touching `Orders` | Phase 7 | Only live SPR path change in the programme |

---

## Remaining blockers

| # | Blocker | Blocks | Owner | Status |
|---|---|---|---|---|
| 1 | **Operational approval** for A5, B7 | Stages A, B | **PO** | Outstanding — nothing executed |
| 2 | `age` vs `gpg` decision | A1 | **PO** | Outstanding |
| 3 | Recovery custodian named | A2 | **PO** | Outstanding |
| 4 | SPR password rotation conversation | R0.10 | **PO + SPR** | Not started; slowest item |

**Not blockers for Phase 1**, though they block later phases:

| Item | First blocks | Note |
|---|---|---|
| Amazon seller registration | Phase 2 live gate | Start now; mock gates pass without it |
| Amazon Business access | Phase 7 | 4–6 week published estimate; start now |
| Resale/dropship policy review | Phase 7 | No engineering input; start now |
| M360 receiver capacity | Phase 4 | The one place M360 is on the critical path |
| Droplet capacity gate | Phase 3 | 8 GB shared with M360, swap in use |

**Phase 1 development can begin immediately.** Stages C and D need no approval and no
Amazon access. Only Stages A and B — which touch the deployed environment — are gated,
and they gate credential *writes*, not the build.
