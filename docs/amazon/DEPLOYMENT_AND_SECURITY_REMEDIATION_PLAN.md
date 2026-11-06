# Deployment and Security Remediation Plan

**Date:** 2026-11-06 · **Revision 2 — decisions applied.** **Status:** approved in principle; **No deployment change or credential
rotation has been made.** · **Target:** droplet `164.90.153.14` (test)

---

## 0. Executive summary — verification changed this plan

I inventoried the actual data before writing procedures. Three facts materially reduce the
risk and the work:

| Expected | Actually found |
|---|---|
| Ciphertext encrypted under the dev key needs migrating | **No ciphertext exists.** `Organization.PortalApiKey` is NULL for the only org; `TradingPartners.TransportCredentialsJson` is plaintext, not `enc:v1:` |
| Key change risks breaking SPR | **It cannot.** SPR reads its credentials through `Unprotect`'s legacy-plaintext passthrough. Changing the key does not touch plaintext |
| Webhook signature change needs a compatibility window | **No consumers exist.** `WebhookSubscriptions` = 0 rows, `WebhookDeliveries` = 0 rows in this environment |

**Consequence: A1b is a configuration-only change with zero data migration and zero SPR
impact.** It is also urgent for a different reason than assumed — not to protect existing
data, but to be in place *before* the first ciphertext is ever written.

**One new finding, not previously reported.** SPR's transport credentials are stored in the
clear. `TransportCredentialsJson` on partner 1 contains the keys `SftpPassword` and
`WebServicesPassword` as plaintext JSON, in a database shared with Merchant360. That is a
separate defect from A1b and is addressed in Part 1C.

---

## 1. Protected-field inventory

Every call site of `ICredentialProtector`, from the code:

| Field | Written by | Read by | State on the droplet |
|---|---|---|---|
| `Organizations.PortalApiKey` | `AdminOrganizationsController.cs:133,261` | `Merchant360ConfigWarningService`, callback delivery | **NULL** — 1 org, no value |
| `TradingPartners.TransportCredentialsJson` | `PublicTradingPartnersController.cs:243` | `SprAdapter.cs:55`, `SprXmlDocumentProcessingService.cs:238,509`, `SprWebServiceContextResolver.cs:43`, `PublicTradingPartnersController.cs:154,240` | **Plaintext JSON**, 61 bytes, keys `SftpPassword` + `WebServicesPassword` |

No other field is protected. There is **no third consumer** to discover later.

### The live risk window

Although nothing is encrypted today, two endpoints *write* ciphertext on demand:

- `PUT` partner transport config → encrypts `TransportCredentialsJson` under the current key.
- Admin org create/update with `ExternalPortalEnabled` → encrypts `PortalApiKey`.

**If either is used before the key is set, that value is encrypted under the dev key and
becomes a real migration problem.** This is why Part 1 is sequenced first and why the
operator freeze in 1A.2 matters.

---

## Part 1 — Encryption key remediation (A1b)

**Change class: configuration only.** No schema change. No existing ciphertext to rewrite.

### 1A. Preparation

| # | Step | Command / detail |
|---|---|---|
| 1A.1 | Re-confirm the inventory immediately before executing — data may have changed since this plan | the two SELECTs in §1, expecting NULL and a non-`enc:` prefix |
| 1A.2 | **Freeze the two writing endpoints** for the window: announce no partner-transport edits and no org portal-key changes | operational, not technical |
| 1A.3 | Full backup | `ssh root@164.90.153.14 /root/droplet/backup.sh` |
| 1A.4 | Verify the backup wrote and is non-trivial | check the reported size; confirm `partnerconnect` dump present |
| 1A.5 | Generate a 32-byte key **off the droplet**, on the operator's machine | `openssl rand -base64 32` |
| 1A.6 | Record it in the team password manager **before** deploying it. Losing it after ciphertext exists means unrecoverable credentials | — |

The key is a base64 string; `AesCredentialProtector` runs it through SHA-256 to derive the
AES key, so any high-entropy value works. It must be **identical** in `pc-api` and
`pc-workers` or values written by one will not decrypt in the other.

### 1B. Deployment

| # | Step | Detail |
|---|---|---|
| 1B.1 | Back up both env files | `cp -a /etc/merchant360/env/pc-api.env{,.bak.$(date +%s)}` and the same for `pc-workers.env` |
| 1B.2 | Append `CredentialEncryption__EncryptionKey=<key>` to **both** files | file mode stays `root:root 0600` |
| 1B.3 | Confirm both files carry the **same** value | compare hashes of the line, not the value in terminal output |
| 1B.4 | Restart `pc-api`, wait for health, then `pc-workers` | API first: it seeds and serves; workers reconnect cleanly |
| 1B.5 | Confirm the fallback warning is **gone** from both logs | `journalctl -u pc-api --since "5 min ago" \| grep -i "development fallback"` → expect no match |

SPR is unaffected at every step: its credentials are plaintext and read through the
passthrough. No SPR process needs to stop.

### 1C. Encrypting the SPR credentials (separate, optional, do second)

This **is** a data change and should be a distinct, separately approved step — not folded
into 1B.

Because `Unprotect` accepts both plaintext and `enc:v1:`, the migration is forward-safe and
needs no downtime:

1. Read the current plaintext value and keep a copy in the password manager.
2. Re-save it through `PUT /api/v1/trading-partners/{id}/transport` so the controller
   encrypts it with the now-correct key.
3. Confirm the stored value begins `enc:v1:`.
4. Run the SPR smoke tests in §4D **before** declaring success.

Rollback: write the plaintext value back; the passthrough still reads it.

**Sequencing constraint.** The strict `Unprotect` from Phase 1 task 1.3 — which throws on a
non-`enc:v1:` value — must **not** be applied to the SPR read path until 1C is complete, or
SPR breaks instantly. Apply strict mode to the new `IntegrationCredential` store only;
leave the legacy path lenient until every legacy field is migrated.

### 1D. Portal API credential rotation

`PortalApiKey` is NULL, so **there is nothing exposed and nothing to rotate** in this
environment. The earlier concern that a real key sat encrypted under a published dev key
does not hold here.

If any other environment has a non-NULL value, treat it as compromised: issue a new key
with the org, set it after 1B, and confirm callback delivery.

`PortalApiKeyHash` (inbound) is a SHA-256 hash, not reversible and not protector-encrypted.
It is unaffected by key changes.

### 1E. Fail-closed startup

Code change, deployed separately from the config change above — in that order, so the guard
never fires against a host that has not yet been configured.

```
if key missing:
    Development            -> warn, use fallback (unchanged)
    any other environment  -> throw at startup
```

Verification: with the key removed in a scratch environment, the service must refuse to
start and log the reason. Deploy only after 1B is confirmed on every environment.

### 1F. Verification

| Check | Expected |
|---|---|
| No "development fallback" warning in either service log | absent |
| Round-trip across processes: encrypt in `pc-api`, decrypt in `pc-workers` | identical plaintext |
| Tampered ciphertext | `AuthenticationTagMismatchException`, not silent garbage |
| SPR SFTP poll | unchanged, succeeds |
| `/health` on all four PC services | 200 / 302 as before |

### 1G. Rollback

Restore the `.bak` env files and restart. Safe because **no ciphertext was created under
the new key** at step 1B. Once 1C has run, rollback additionally requires restoring the
plaintext credential value — which is why 1C is separate and separately approved.

---

## Part 2 — Webhook signature correction (A5)

**Change class: code only.** No schema, no data.

### 2A. Consumer inventory

`WebhookSubscriptions` = **0 rows**. `WebhookDeliveries` = **0 rows**.

**No consumer can break**, because none exists. The versioned compatibility migration below
is therefore cheap insurance for other environments and for future consumers, not a
constraint on this one. I am not proposing a dual-signing window for a system with no
subscribers — that would be ceremony.

### 2B. Corrected specification

As approved in contract v1.1 §2:

```
signingString = <X-PC-Signature-Timestamp> + "." + <raw body bytes>
signature     = "v1=" + lowercase_hex( HMAC_SHA256(secret, utf8(signingString)) )
```

Headers: `X-PC-Signature-Timestamp` (RFC 3339 UTC), `X-PC-Signature-KeyId`,
`X-PC-Signature`. Verify in order: headers present → timestamp within ±5 min → key id
resolves → constant-time compare over **raw** bytes → only then deserialise.

### 2C. Versioned migration (for environments that do have consumers)

1. Emit **both** headers: legacy `X-Webhook-Signature` (body-only) and new `X-PC-Signature`.
2. Consumers migrate to the new header at their own pace.
3. After the agreed window, stop emitting the legacy header.

Shipping step 3 immediately is acceptable **in this environment only**, on the evidence
above. Record that as a decision, not an oversight.

### 2D. Replay, rotation, failure

- **Replay:** ±5 minutes, rejected not clamped; the timestamp is inside the signed message,
  which is the whole point of the fix.
- **Rotation:** `KeyId` selects the secret; publish with the new one while both verify;
  retire the old id after the overlap.
- **Failure:** signature or timestamp failure is **401 with no retry** — retrying an
  unsigned body cannot succeed. Transport failures retry with backoff and dead-letter on
  exhaustion.

### 2E. Regression tests

Valid · tampered body · **tampered timestamp** (the defect being fixed) · replay outside
window · unknown key id · old key during overlap · re-serialised body must fail · shared
test vector `a050819d…eedd0c` reproduced by both repositories.

---

## Part 3 — Transactional outbox (A3)

**Change class: code only.** No schema, no data.

### 3A. The boundary

Today `OutboxRepository.AddAsync` calls `SaveChangesAsync` itself, so `EnqueueAsync`
commits on its own. Snapshot and event currently land together only because both
repositories share the scoped `DbContext` — incidental, and silently breakable.

Add an **overload that does not commit**:

```
EnqueueAsync(...)                       // unchanged: adds and commits
EnqueueInCurrentTransactionAsync(...)   // adds to the context, does NOT save
```

The caller opens a transaction via the existing `IUnitOfWork`, writes the sales-order
snapshot, enqueues within it, and commits once.

### 3B. Preserving SPR

The existing `EnqueueAsync` keeps its exact behaviour and signature. Every current caller —
the SPR document pipeline, the M360 callbacks, webhook delivery — is **untouched and
unrecompiled in behaviour**. Only new Amazon code uses the overload. This is additive by
construction.

### 3C. PostgreSQL-backed tests

Against real PostgreSQL, not InMemory, which does not honour transactions:

| Test | Assertion |
|---|---|
| Commit | Snapshot row **and** outbox row both present |
| Rollback after enqueue | **Neither** present — no orphan event |
| Rollback after snapshot, before enqueue | Neither present |
| Existing `EnqueueAsync` path | Still commits independently — SPR behaviour unchanged |
| Concurrent enqueues on separate contexts | Both land; no interference |

---

## Part 4 — Deployment safety

### 4A. Pre-deployment checklist

- [ ] Plan reviewed and approved
- [ ] Protected-field inventory re-confirmed against live data (§1A.1)
- [ ] Writing endpoints frozen for the window (§1A.2)
- [ ] `backup.sh` run and output verified
- [ ] Env files backed up with timestamped copies
- [ ] Key generated off-host and stored in the password manager
- [ ] Solution builds; 175 unit tests green
- [ ] `validate_contracts.py` green
- [ ] Rollback steps rehearsed and understood by whoever is executing

### 4B. Backups — two distinct kinds

| Kind | Command | Covers |
|---|---|---|
| Database + env + uploads | `/root/droplet/backup.sh` | Both PC and M360 databases, `/etc/merchant360/env`, uploads |
| Env files only | `cp -a …env{,.bak.$TS}` | Fast targeted rollback for a config-only change |

The droplet backup also covers Merchant360. Confirm its reported size looks normal before
proceeding — a truncated backup is worse than none because it invites false confidence.

### 4C. Restart sequence

```
1. pc-api        restart → wait for /health 200
2. pc-workers    restart → wait for /health 200
3. (no restart)  pc-adminportal, pc-customerportal — they hold no credential key
4. (no restart)  m360-*      — untouched by this work
```

API before workers: workers reconnect to the database independently and have no inbound
traffic to drain. Neither portal uses `ICredentialProtector`, so neither needs restarting.

### 4D. Smoke tests

**SPR — must be unchanged:**
- SFTP poll runs without a credential error in `pc-workers`
- SPR web-service resolver obtains credentials (`SprWebServiceContextResolver`)
- Outbound EZPO4 generation unchanged
- EDI 850/855/856/810 round trip green

**Merchant360 — must be unchanged:**
- `/api/integrations/orders` responds as before
- The four outbox message types serialise byte-identically (`Merchant360WireContractTests`)
- Price-feed push path unaffected

**Platform:**
- `/root/droplet/health.sh` all green
- No new errors in 15 minutes across `pc-api` and `pc-workers`

### 4E. Feature flags and rollback criteria

The key change has no flag — it is configuration, and partial application is the dangerous
state. Flags belong on the code changes:

| Change | Flag | Default |
|---|---|---|
| Fail-closed startup (1E) | none — environment-conditional by design | on outside Development |
| New signature emission (2B) | `Webhooks:EmitV1Signature` | off until consumers exist |
| Outbox overload (3A) | none — new code path, unused until Amazon work | — |

**Roll back immediately if:** any SPR credential error appears · any service fails to start ·
the SPR SFTP poll fails · any M360 wire-contract test fails · health checks do not return to
green within 10 minutes.

### 4F. Post-deployment monitoring

First hour: `pc-api` and `pc-workers` logs for credential and decryption errors; SPR poll
success; health every 15 minutes. First 24 hours: confirm the scheduled SPR jobs complete a
full cycle, and that no "development fallback" warning has reappeared after any restart.

---

## 5. Change classification

The distinction the review asked for, stated explicitly:

| Change | Schema | Encrypted data | Code | Config |
|---|---|---|---|---|
| 1B — set the encryption key | — | — | — | **yes** |
| 1C — encrypt SPR credentials | — | **yes** | — | — |
| 1D — rotate portal keys | — | not applicable here | — | — |
| 1E — fail-closed startup | — | — | **yes** | — |
| 2 — signature correction | — | — | **yes** | yes (flag) |
| 3 — outbox overload | — | — | **yes** | — |

**No schema migration appears anywhere in this plan.** The first schema change in the
programme is Phase 1's `AddIntegrationSubstrate`, which is additive and separate from this
remediation.

## 6. Sequencing

```
1A prep ─► 1B key ─► 1F verify ─► 1E fail-closed ─► 1C encrypt SPR creds (separate approval)
                        │
                        ├─► 2 signature  (independent, no consumers)
                        └─► 3 outbox     (independent, code only)
```

1B before everything: it is the precondition for any credential being written correctly.
1E after 1B so the guard never fires against an unconfigured host. 1C last because it is
the only step that mutates data and the only one whose rollback needs a stored value.

Parts 2 and 3 are independent of the key work and of each other, and can proceed in
parallel with Phase 1 development.

## 7. Approved decisions

| # | Decision | Effect on this plan |
|---|---|---|
| **1** | SPR credential encryption (1C) approved as a **separate, controlled change** after the key is installed and verified | 1C now requires: consumer confirmation, backup/rollback rehearsal, SPR regression afterwards, **and** an evaluation of rotating the SPR passwords themselves |
| **2** | Test environment goes **straight to the corrected signature**, after confirming no undocumented consumers. Other environments must not assume the same | 2C.3 may ship immediately here; versioned signing support is retained in code |
| **3** | Password manager is the authoritative key store; restricted admin access; **independent recovery custodian**; unique high-entropy key per environment; runtime keys in protected config outside source control | 1A.5–1A.6 updated; custody roles named below |

### 7.1 Decision 1 — consumer confirmation for encrypted SPR values

All six read sites call `Unprotect`, which accepts both `enc:v1:` and legacy plaintext, so
every consumer already supports encrypted values:

| Consumer | File |
|---|---|
| `SprAdapter` | `PartnerAdapters/SPR/SprAdapter.cs:55` |
| `SprXmlDocumentProcessingService` (poll) | `Infrastructure/SprContent/SprXmlDocumentProcessingService.cs:238` |
| `SprXmlDocumentProcessingService` (send) | same, `:509` |
| `SprWebServiceContextResolver` | `Infrastructure/Services/SprWebServiceContextResolver.cs:43` |
| `PublicTradingPartnersController` (read) | `API/Controllers/V1/PublicTradingPartnersController.cs:154,240` |

No consumer requires modification for 1C. The only hazard is the strict-mode change
(Phase 1 task 1.3), which must stay off this path until 1C completes.

### 7.2 Decision 1 — rotating the SPR passwords themselves

**Recommended: rotate.** `SftpPassword` and `WebServicesPassword` have been stored in
plaintext in `partnerconnect.dump` in **every nightly backup**. There are 19 retained sets.
Encrypting the live column does nothing about those copies.

Rotation is a change on **S.P. Richards' side** and needs their cooperation, so treat it as
a scheduled task rather than part of this deployment:

1. Request new SFTP and web-service credentials from SPR.
2. Install them through the transport endpoint **after** the key is set, so they are written
   encrypted from the outset and never exist as plaintext in a dump.
3. Run the SPR regression suite.
4. Let the 14-day retention expire the old backups, or prune them deliberately.

Until rotation happens, the plaintext credentials in historical backups remain exposed to
anyone who can read those backups — which §8 is about.

### 7.3 Decision 3 — key custody

| Role | Holder | Access |
|---|---|---|
| Authoritative store | Team password manager | Restricted admin group |
| Recovery custodian | A named person **outside** the deploying admin group | Break-glass only, logged |
| Runtime copy | `/etc/merchant360/env/*.env`, `root:root 0600` | Service identity only |
| Source control | **never** | — |

One unique 32-byte key per environment. Losing it after ciphertext exists means every
protected credential must be re-issued — hence the independent custodian.

---

## 8. Backup separation (new requirement) — investigated

**The concern is confirmed, and the evidence is unambiguous.**

### 8.1 What I found

`deploy/droplet/backup.sh` writes each nightly set to `/var/backups/merchant360/<stamp>/`
containing **both halves**:

| File | Contains |
|---|---|
| `partnerconnect.dump`, `merchant360.dump` | Every row — including any ciphertext |
| `env.tar.gz` | The env files — **including `CredentialEncryption__EncryptionKey`** |

On the droplet right now: **19 of 19 backup sets contain both** a database dump and
`env.tar.gz`. The script's own header acknowledges it holds "the credential-encryption key"
and that it "cannot be regenerated".

A second, separate weakness in the same place:

```
drwx------  /var/backups/merchant360          <- 0700, the only real control
drwxr-xr-x  /var/backups/merchant360/<stamp>  <- 0755
-rw-r--r--  partnerconnect.dump               <- 0644, world-readable
-rw-------  env.tar.gz                        <- 0600
```

The dumps are world-readable; only the 0700 parent prevents a non-root local user reading
every row of both databases. The env tarball is tighter than the data it unlocks — which is
backwards, because the data is the larger prize.

### 8.2 Why it matters more after this remediation

Today the exposure is plaintext SPR credentials — bad, but the key is irrelevant because
nothing is encrypted. **After the key is installed and Amazon refresh tokens are stored,
every backup set becomes a complete, self-contained compromise**: ciphertext plus the key
that opens it, in one directory, copied nightly.

Three realistic paths by which a set leaves the host, all of them currently carrying both
halves:

1. The restore rehearsal documented in `backup.sh` — `rsync -az` of the whole directory to an operator's laptop.
2. A DigitalOcean volume snapshot or droplet image.
3. The planned object-storage push, which the script's closing comment anticipates.

### 8.3 Revised strategy — encrypt the env tarball to a key the droplet does not hold

Asymmetric encryption. The droplet gets a **public** key and can only encrypt; the private
key lives in the password manager with the recovery custodian.

```
age-keygen            -> private key -> password manager (custodian)
                      -> public key  -> /etc/merchant360/backup-recipient.pub (0644)

backup.sh:  tar -czf - env/ | age -R /etc/merchant360/backup-recipient.pub > env.tar.gz.age
```

What this achieves:

| Property | Before | After |
|---|---|---|
| Backup set contains the decryption key | **Yes** | No — only a tarball nobody on the host can open |
| Root on the droplet can read env | Yes (live files) | Yes (live files) — unchanged, and not the threat model |
| Stolen backup set yields credentials | **Yes** | No — ciphertext without a key |
| Restore self-sufficient | Yes | Yes, with the custodian's private key |
| Off-site copy safe to hold | No | Yes |

The live `/etc/merchant360/env/*.env` files are untouched; services read them as now.

### 8.4 Also required

| # | Change | Reason |
|---|---|---|
| 8.4.1 | `chmod 700` each backup subdirectory; `chmod 600` the dumps | Dumps should not be world-readable even under a 0700 parent — defence in depth |
| 8.4.2 | Verify `age` (or `gpg`) is installed, or vendor a static binary | `backup.sh` must not fail silently if the tool is missing — it uses `set -euo pipefail`, so an absent binary aborts the backup |
| 8.4.3 | **Rehearse the restore** with the custodian's key before relying on it | An unopenable backup is not a backup. Test on a scratch database |
| 8.4.4 | When off-site storage is added, push the **encrypted** env tarball, never the plaintext one | Closes the path the script's own TODO opens |
| 8.4.5 | Document that DO snapshots capture the live env files regardless | Snapshot access must be restricted to the same admin group; this control is outside `backup.sh` |

### 8.5 Sequencing against the key deployment

**8.3 and 8.4.1–8.4.3 must land before 1B**, not after. Installing the encryption key into a
backup régime that copies it beside the data every night would create the exact exposure
this requirement exists to prevent — and every night of delay adds another retained set.

Revised order:

```
8.3 + 8.4 (backup separation, rehearsed)
      └─► 1B (install key) ─► 1F verify ─► 1E fail-closed ─► 1C encrypt SPR creds
                                                                   └─► 7.2 rotate SPR passwords (SPR's schedule)
```

### 8.6 Access control summary

| Artefact | Location | Who |
|---|---|---|
| Live env files | `/etc/merchant360/env` 0600 | root / service identity |
| Backup DB dumps | `/var/backups/merchant360/<stamp>` 0700, files 0600 | root |
| Backup env tarball | same directory, `age`-encrypted | **nobody on the host** |
| Private recovery key | Password manager | Recovery custodian, break-glass, logged |
| Authoritative key copy | Password manager | Restricted admin group |

---

## 9. Open items for the approver

1. **`age` vs `gpg`** for 8.3. `age` is simpler and has no keyring state; `gpg` is likelier to be installed already. Either satisfies the requirement — pick one and I will write the exact `backup.sh` change.
2. **SPR password rotation timing** (7.2) depends on S.P. Richards. Worth opening that conversation now, since it is the slowest item and the one that closes the historical-backup exposure.
3. **Operational approval to execute** remains outstanding. Nothing in this plan has been run.
