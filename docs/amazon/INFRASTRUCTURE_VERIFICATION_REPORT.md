# Infrastructure verification report

**Date:** 2026-11-05 · **Method:** direct inspection of the PartnerConnect repository and the
deployed droplet environment. Findings are from code and configuration, not from the
architecture document.

> Three of the seven v1.0 assumptions did not hold. One is a **production security defect
> that exists today**, independent of Amazon.

| # | Assumption | Verdict |
|---|---|---|
| A1 | `AesCredentialProtector` is AEAD with safe nonce handling | **Verified and reusable** |
| A1b | Its key is configured in the deployed environment | **FAILED — critical** |
| A2 | `RawDocumentArchive` supports redaction / retention / deletion | **Missing** |
| A3 | `OutboxService` supports atomic snapshot + event persistence | **Requires modification** |
| A4 | Scheduled jobs handle many connections without starvation | **Requires modification** |
| A5 | `WebhookDeliveryService` signing supports the corrected contract | **Requires modification** |
| A6 | Authorization / tenant resolution supports the new endpoints | **Verified and reusable** |
| A7 | PostgreSQL can enforce the required identities | **Verified and reusable** |

---

## A1 — `AesCredentialProtector`: verified

`src/Vendorea.PartnerConnect.Infrastructure/Security/AesCredentialProtector.cs`

AES-GCM, 256-bit key, fresh 12-byte nonce per operation from `RandomNumberGenerator`,
16-byte tag, versioned `enc:v1:` prefix, format `base64(nonce || tag || ciphertext)`.
That is correct authenticated encryption. **My earlier concern that this might be raw CBC
was wrong** — no change needed to the algorithm.

Two residual weaknesses, both to address before refresh tokens are stored:

1. **Key derivation is a bare `SHA256(configuredString)`** — not a KDF. Fine if the
   configured value is a high-entropy random key; weak if someone types a passphrase.
   Document the requirement, or derive with PBKDF2 and a salt.
2. **`Unprotect` passes through anything without the `enc:v1:` prefix**, returning it
   unchanged as "legacy plaintext". For a refresh token that silently converts a failure
   to encrypt into a value that still works — the failure would never surface. Credentials
   need a strict mode that throws instead.

## A1b — The encryption key is not set in the deployed environment: **critical**

`CredentialEncryption__EncryptionKey` is **absent from `pc-api.env` and `pc-workers.env`**
on the droplet. I grepped both files for any credential- or encryption-named setting; there
is none.

The class falls back to a **hard-coded development key committed in the source**, logging
only a warning:

```csharp
private const string DevFallbackKey = "pc-dev-credential-encryption-key-not-for-production";
```

Consequences today, before any Amazon work:

- `Organization.PortalApiKey` — the outbound key PC presents to each org's portal — is
  encrypted in the test database with a key published in the repository.
- Anyone with the database and the source can recover it. The ciphertext is not protection.

**This is a Phase 1 blocker.** Amazon refresh tokens are long-lived credentials to a
seller's revenue-generating account; they must not be written until a real key is
configured and existing values re-encrypted.

Remediation: generate a 32-byte random key per environment, set it on both services,
re-encrypt existing values, and make the fallback **fail closed** outside Development.

## A2 — `RawDocumentArchive`: missing, not merely unextended

`src/Vendorea.PartnerConnect.Domain/Entities/RawDocumentArchive.cs`

The entity has `RetentionPolicy`, `ExpiresAt`, `ContentHash`, inline-or-blob storage, and
an index on `ExpiresAt`. It looks ready.

It is not. Searching the solution, `RawDocumentArchive` appears only in the DbContext, its
EF configuration, the migrations, and as a navigation property on `PartnerDocument`.
**Nothing writes a row, nothing reads one, and no purge job acts on `ExpiresAt`.** There is
no repository and no service.

So v1.0's "reused unchanged" was doubly wrong: it is neither unchanged nor reused. Treat it
as **new work** — writer, retention enforcement, redaction, and an access-controlled read
path — or choose a different archive mechanism. Either way it is not free.

## A3 — Outbox atomicity: requires modification

`OutboxService.EnqueueAsync` → `OutboxRepository.AddAsync`:

```csharp
_context.OutboxMessages.Add(message);
await _context.SaveChangesAsync(cancellationToken);   // commits here
```

The repository commits on its own. The contract requires the sales-order snapshot and its
outbox row to land in **one** transaction.

Today they *would* commit together — both repositories share the scoped `DbContext`, so
`SaveChangesAsync` flushes everything pending. But that is **incidental, not guaranteed**:
it breaks silently the moment a caller saves the snapshot first, or a future change gives
the outbox its own context. Relying on it is exactly the kind of accidental coupling that
produces an order with no event, or an event for an order that was rolled back.

`IUnitOfWork` already exists with `BeginTransactionAsync` semantics
(`src/Vendorea.PartnerConnect.Persistence/UnitOfWork/UnitOfWork.cs`) but does **not** expose
the outbox repository. Phase 1 adds an enqueue overload that participates in a caller
transaction without committing.

## A4 — Scheduler fairness: requires modification

`ScheduledJobRepository.TryClaimAsync(jobId, …)` claims a **job row**, not a unit of work.
One job key fanning out over N connections iterates them inside a single handler run, so a
slow, throttled or revoked connection delays every connection behind it and can exhaust the
run window.

Options for Phase 3 (not Phase 1): a claimable work row per connection, or bounded
concurrency with a per-connection circuit breaker and rotation of the starting offset so
the same connection is not always last.

## A5 — Webhook signing: requires modification, and today's code has the C1 defect

`src/Vendorea.PartnerConnect.Webhooks/Services/WebhookService.cs:336`

```csharp
private static string GenerateSignature(string payload, string secret)
{
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
    return Convert.ToHexString(hash).ToLowerInvariant();
}
```

The signature covers the **payload only**. `X-Webhook-Timestamp` is sent
(`WebhookDeliveryService.cs:170`) but is not part of the signed message.

So a captured body can be replayed indefinitely with any timestamp the attacker chooses —
the timestamp header provides no protection because nothing binds it to the signature.
This is **the C1 defect, already shipped**, affecting existing outbound webhooks, not just
the planned Amazon events.

The helper cannot be reused as-is. Phase 1 introduces the corrected construction
(`timestamp + "." + raw body`, key id, constant-time compare) as shared code, and the
existing webhook path should migrate to it with a versioned overlap.

## A6 — Tenant resolution: verified

`OrgController.ResolveTenantAsync` is a single choke point: it checks the tenant belongs to
the organization and that a scoped portal user holds it, returning **404 rather than 403**
so tenant existence cannot be probed. `ResolveTenantPartnerAsync` layers the approved-
connection requirement on top. New Amazon endpoints inherit correct isolation by routing
through the same helper. No change required.

## A7 — PostgreSQL constraints: verified

EF configurations already express unique indexes and filtered indexes, and the
`DatabaseTests` project runs against real PostgreSQL via Testcontainers — so
`(IntegrationConnectionId, ExternalOrderId)` and the partial unique index for
`(TenantId, ProviderId, ExternalAccountId) WHERE ExternalAccountId IS NOT NULL` can both be
proven rather than assumed. Two cautions from the repo's own history apply: declare defaults
in the entity configuration (not only `defaultValue:` in the migration), and give any
value-converted collection a `ValueComparer`.
