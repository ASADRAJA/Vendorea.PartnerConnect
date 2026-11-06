# Key custody and recovery procedure

**Status: PREPARED, NOT EXECUTED.** No key has been generated, installed or rotated.

Two independent keys. Confusing them is the most likely operational error, so they are
named differently everywhere:

| Key | Protects | Where it lives | Form |
|---|---|---|---|
| **Backup recovery key** (`age`) | Backup sets | Password manager; public half on the droplet | X25519 keypair |
| **Application encryption key** | `IntegrationCredential.ProtectedValue`, `Organization.PortalApiKey` | Password manager; value in `*.env` on the droplet | 32 random bytes, base64 |

The droplet holds the **public** backup key and can only encrypt. It holds the application
key in full, because services must decrypt at runtime — which is exactly why backups must
not carry it.

## Custodians

| Role | Holds | May act |
|---|---|---|
| **Primary custodian** | Both private keys in the password manager | Routine recovery, rotation |
| **Secondary custodian** | Independent access to both | Break-glass when the primary is unavailable |
| Deploying admin | Application key only, to install it | No access to the backup private key |

Both custodians are named before Stage A runs. The secondary is deliberately **not** in the
deploying admin group: an admin who can both read a backup and decrypt it reintroduces the
single point of compromise this design removes.

Neither private key is ever placed in the repository, on the droplet, in CI, or in a ticket.

## Generation

```bash
# Backup recovery keypair — on the custodian's machine, never the droplet
age-keygen -o pc-backup-recovery.key      # private -> password manager, then shred local copy
grep "public key:" pc-backup-recovery.key # public  -> /etc/merchant360/backup-recipient.pub

# Application encryption key — one per environment, never shared between them
openssl rand -base64 32
```

Record in the password manager before deploying: environment, creation date, custodians,
and where the runtime copy lives. A key whose location is undocumented is a key nobody can
rotate.

## Rotation

**Backup key:** generate a new pair, install the new public key, let both private keys be
retained until the old sets age out at 14 days, then retire the old one. No re-encryption of
existing sets is needed — they simply expire.

**Application key:** harder, because ciphertext exists. Decrypt with the old key and
re-encrypt with the new, in one pass, with services stopped. **Do not rotate while
`IntegrationCredential` rows exist without a rehearsed procedure** — a partial pass leaves
rows that no key opens.

## Loss

| Lost | Consequence | Recovery |
|---|---|---|
| Backup private key | Existing sets unreadable; live system unaffected | New keypair; sets age out in 14 days |
| Application key, no ciphertext | None | Generate a new one |
| **Application key, ciphertext exists** | **Protected credentials unrecoverable** | Re-issue every credential: new Amazon consent, new portal keys |

The third row is why there is a secondary custodian.

## Historical backup sets — the 19 that exist today

All 19 contain plaintext SPR credentials inside `partnerconnect.dump`, and the application
key inside `env.tar.gz`. Encrypting future sets does nothing about them.

**No backup may be deleted without explicit operational approval**, so the plan is retention
and rotation, not deletion:

| # | Step | Owner | Notes |
|---|---|---|---|
| H1 | Confirm `/var/backups/merchant360` is `0700` and no copy exists off-host | PC | If any set was copied to a laptop for a restore rehearsal, treat those credentials as disclosed |
| H2 | **Do not delete.** Let the 14-day retention expire them naturally | — | Deletion needs approval; expiry does not |
| H3 | Re-encrypt in place rather than waiting, **if approved** | PC | `age -R` each existing set, then shred the plaintext. Reversible only while the originals exist, so verify each before shredding |
| H4 | **Rotate the SPR SFTP and web-service passwords** | PO + SPR | The only step that actually invalidates what those dumps contain |
| H5 | Rotate the application key after H4 | PC | Only once; no ciphertext exists yet, so it is cheap today and expensive later |

**H4 is the one that matters.** Re-encrypting old backups reduces exposure; rotating the
credentials ends it. Until SPR issues new passwords, anyone who obtained a historical set
has working credentials, and no amount of encryption applied afterwards changes that.

H3 is optional and lower value than H4 — it protects against future disclosure of sets that
have already been taken, which is a narrower threat than the one H4 closes.
