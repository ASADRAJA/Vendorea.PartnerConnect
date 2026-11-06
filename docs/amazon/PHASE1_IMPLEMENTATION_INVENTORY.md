# Phase 1 — implementation inventory

**Phase 1: integration provider and connection foundation.**
Amazon-independent. No Amazon API is called in this phase; no Amazon credentials are stored.

**Gate:** two connections for one tenant are isolated in data and credentials; a second
tenant cannot see either; credentials round-trip through a real key across a process
restart; the full SPR regression is green.

**Prerequisite — blocking:** finding **A1b**. A real encryption key must be configured and
existing values re-encrypted before any credential row is written. Phase 1 task 1.1.

Complexity: **S** ≈ ½–1 day · **M** ≈ 1–3 days · **L** ≈ 3–5 days.

---

## 1. Security remediation (blocking, do first)

| # | Task | Files | Kind | Cx |
|---|---|---|---|---|
| 1.1 | Generate a 32-byte key per environment; set `CredentialEncryption__EncryptionKey` on `pc-api` and `pc-workers`; re-encrypt existing `Organization.PortalApiKey` values | droplet env files + one-off tool | **New** | S |
| 1.2 | Make the dev fallback key **fail closed** outside Development | `Infrastructure/Security/AesCredentialProtector.cs` | **Modify** | S |
| 1.3 | Add a strict `Unprotect` that throws on a non-`enc:v1:` value, for credentials only; leave the lenient path for legacy columns | same | **Modify** | S |
| 1.4 | Extract the corrected HMAC signer (`timestamp + "." + raw body`, key id, `FixedTimeEquals`) as shared code | **New** `Infrastructure/Security/IntegrationSignature.cs` | **New** | M |

*Acceptance:* a value encrypted by `pc-api` decrypts in `pc-workers` after restart; a
tampered ciphertext throws; the service refuses to start in Production without a key; the
signer reproduces the shared test vector `a050819d…eedd0c`.

## 2. Domain entities

| # | Task | Files | Kind | Cx |
|---|---|---|---|---|
| 2.1 | `IntegrationProvider` | **New** `Domain/Entities/Integration/IntegrationProvider.cs` | New | S |
| 2.2 | `IntegrationConnection` (`TenantId` non-nullable) | **New** `.../IntegrationConnection.cs` | New | S |
| 2.3 | `IntegrationCredential` (separate table; never projected on read paths) | **New** `.../IntegrationCredential.cs` | New | S |
| 2.4 | `IntegrationSyncState` (watermark per connection **per capability per marketplace**) | **New** `.../IntegrationSyncState.cs` | New | S |
| 2.5 | `IntegrationCapabilities` flags enum | **New** `.../IntegrationCapabilities.cs` | New | S |

`PartnerCapability`, `TradingPartner` and `TenantPartnerAccount` are **not touched**.

## 3. Persistence

| # | Task | Files | Kind | Cx |
|---|---|---|---|---|
| 3.1 | Four EF configurations; defaults declared **in configuration**, not only in the migration | **New** `Persistence/Configurations/Integration/*.cs` | New | M |
| 3.2 | Partial unique index `(TenantId, IntegrationProviderId, ExternalAccountId) WHERE ExternalAccountId IS NOT NULL` — **C3**; pending rows have no account id | same | New | M |
| 3.3 | Register 4 `DbSet`s | `Persistence/PartnerConnectDbContext.cs` | **Modify** | S |
| 3.4 | Repositories; credential repo exposes **no** method returning `ProtectedValue` on a list path | **New** `Persistence/Repositories/Integration*.cs` | New | M |
| 3.5 | Register repositories in DI | `Persistence/DependencyInjection.cs` | **Modify** | S |
| 3.6 | Migration `AddIntegrationSubstrate` — additive only, no existing table altered | **New** `Persistence/Migrations/*` | New | S |
| 3.7 | Outbox enqueue overload that joins a caller transaction without committing — **A3** | `Application/Services/OutboxService.cs`, `Persistence/Repositories/OutboxRepository.cs`, `Application/Interfaces/IOutboxService.cs` | **Modify** | M |

*Note on 3.7:* additive overload only. The existing `EnqueueAsync` keeps its
commit-on-write behaviour so the SPR outbox path is untouched.

## 4. Application services

| # | Task | Files | Kind | Cx |
|---|---|---|---|---|
| 4.1 | `IIntegrationConnectionService` — create, list, detail, disconnect; enforces reconnect-reuse (**C3**) | **New** `Application/Interfaces` + `Application/Services` | New | M |
| 4.2 | `IIntegrationCredentialStore` — put/get/rotate/revoke, protector-backed, versioned | **New** | New | M |
| 4.3 | `IIntegrationContextFactory` — builds per-call context; **never cached across tenants** | **New** | New | S |
| 4.4 | `IIntegrationAdapter` + capability sub-interfaces (contracts only; no Amazon implementation) | **New** `Contracts/Integration/` | New | S |
| 4.5 | Register in `AddPartnerConnectServices` | `Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` | **Modify** | S |

## 5. API surface

| # | Task | Files | Kind | Cx |
|---|---|---|---|---|
| 5.1 | `IntegrationsController` — the 4 non-OAuth routes from the v1.1 OpenAPI | **New** `API/Controllers/V1/IntegrationsController.cs` | New | M |
| 5.2 | Route every action through the existing `ResolveTenantAsync` choke point (**A6**) | same | New | S |
| 5.3 | Add `integrations:read` / `integrations:write` to `ApiScopes` and `OrganizationDefault` | `Domain/Entities/ApiKey.cs` | **Modify** | S |
| 5.4 | DTOs matching the OpenAPI component schemas exactly | **New** | New | S |

OAuth authorize / callback / disconnect-with-revoke are **Phase 2**, not here.

## 6. Observability

| # | Task | Files | Kind | Cx |
|---|---|---|---|---|
| 6.1 | Audit on connect, disconnect, credential rotation — action and actor, never the secret | reuse `IAuditService` | **Reuse** | S |
| 6.2 | Metrics: connections by status, credential age, failed-decrypt count | `Metering/Models/UsageRecord.cs` (`MetricType` values) | **Modify** | S |
| 6.3 | Admin cross-org connection health view | **New** `API/Controllers/Admin/AdminIntegrationsController.cs` | New | S |

## 7. Tests

| # | Test | Project | Kind | Cx |
|---|---|---|---|---|
| 7.1 | Credential round-trip across a process boundary with a real key | DatabaseTests | New | S |
| 7.2 | Tampered ciphertext throws; strict mode rejects plaintext | UnitTests | New | S |
| 7.3 | Signature: valid, tampered body, **tampered timestamp**, replay, unknown key id, old key in overlap | UnitTests | New | M |
| 7.4 | Partial unique index: two pending rows allowed; two connected rows for one account rejected (**real PostgreSQL**) | DatabaseTests | New | M |
| 7.5 | Reconnect reuses the connection id and preserves watermarks (**C3**) | DatabaseTests | New | M |
| 7.6 | Tenant A cannot read tenant B's connection — expect **404**, not 403 | IntegrationTests | New | M |
| 7.7 | Two connections in one process share no credential or context (**tenant isolation**) | UnitTests | New | M |
| 7.8 | Snapshot + outbox row commit atomically; a rolled-back snapshot leaves no event | DatabaseTests | New | M |
| 7.9 | `ScopesForRole` grants the new scopes to OrgAdmin/TenantManager and withholds `:write` from Viewer | UnitTests | New | S |
| 7.10 | **SPR regression**: existing 175 unit tests green; EDI 850/855/856/810 round trip unchanged | UnitTests | **Reuse** | S |
| 7.11 | `Merchant360WireContractTests` extended to assert all four existing message types byte-unchanged | UnitTests | **Modify** | S |
| 7.12 | `validate_contracts.py` wired into CI | CI config | New | S |

## 8. Explicitly not in Phase 1

Any Amazon API client · OAuth flow and state tokens (Phase 2) · `SalesOrder` (Phase 3) ·
PC→M360 events (Phase 4) · `RawDocumentArchive` remediation — **A2**, deferred to Phase 3
when buyer data first arrives · scheduler fairness — **A4**, deferred to Phase 3 when
per-connection polling begins · any change to `Orders` (Phase 7).

## 9. Ownership

| Work | Owner |
|---|---|
| Everything above | **PartnerConnect** |
| Amazon developer registration (runs in parallel) | **Product owner** |
| Connection management UI calling PC's API | **Merchant360** — may begin once 5.1 is merged |
| Event receiver, inbox, SKU mapping | **Merchant360** — Phase 4, not now |

## 10. Dependency order

```
1.1 ─► 1.2 ─► 1.3 ─┐
1.4 ───────────────┤
2.1-2.5 ─► 3.1-3.6 ─► 4.1-4.4 ─► 5.1-5.4 ─► 6.x
                3.7 ─────────────┘
                                   └─► 7.x (tests follow their subject)
```

1.1 gates every task that writes a credential. Entities gate persistence; persistence gates
services; services gate the API. 3.7 is independent of the entity work and can run in
parallel.

## 11. Phase 1 exit checklist

- [ ] Encryption key set in every environment; no service starts on the fallback outside Development
- [ ] Two connections for one tenant, isolated in data and credentials
- [ ] A second tenant receives 404 for both
- [ ] Credentials survive a restart and decrypt in the other service
- [ ] Reconnecting an existing account reuses its connection row
- [ ] Snapshot + outbox commit atomically, proven on real PostgreSQL
- [ ] Signature implementation reproduces the shared test vector
- [ ] `validate_contracts.py` green in CI
- [ ] 175 existing unit tests green; SPR EDI round trip unchanged
- [ ] Four M360 message types asserted byte-unchanged
