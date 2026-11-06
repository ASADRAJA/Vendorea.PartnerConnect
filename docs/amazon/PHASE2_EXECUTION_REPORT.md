# Phase 2 — Amazon Seller Authorization: Execution Report

**Date:** 2026-11-06
**Branch:** `feature/phase1-integration-substrate`
**Status:** development complete; the phase gate is blocked on external approvals and one
deployment gate (below).

---

## 1. What was built

| Backlog item | Delivered | Where |
|---|---|---|
| 2.1 OAuth state store — single-use, hashed, expiring | ✅ | `IntegrationOAuthState`, `IntegrationOAuthStateService`, `IntegrationOAuthStateRepository` |
| 2.2 Authorize / callback / disconnect endpoints | ✅ | `IntegrationsController.StartAuthorization`, `IntegrationCallbackController.Callback`, existing `Disconnect` |
| 2.3 Reconnect-reuse identity resolution (C3) | ✅ (Phase 1 service, now exercised end to end) | `IntegrationConnectionService.CompleteAuthorizationAsync` |
| 2.4 LWA token exchange + refresh, in-memory cache keyed by connection | ✅ | `AmazonLwaClient`, `AmazonAccessTokenCache`, `AmazonAccessTokenProvider` |
| 2.5 Connection health, denied-capability reporting | ✅ | `RecordCredentialRejectedAsync`, `RecordTransientErrorAsync`, `RecordCapabilityDeniedAsync` |
| 2.6 M360 connect/disconnect UI | **Merchant360** | not PC work |
| 2.7 Tests — matrix 2.1–2.10 | ✅ | see §3 |

### New files

```
src/…Domain/Entities/Integration/IntegrationOAuthState.cs
src/…Application/Interfaces/IIntegrationOAuth.cs
src/…Application/Services/IntegrationOAuthStateService.cs
src/…Application/Services/IntegrationAuthorizationService.cs
src/…Application/Services/IntegrationAuthorizationOptions.cs
src/…Application/Services/AmazonAccessTokenCache.cs
src/…Application/Services/AmazonAccessTokenProvider.cs
src/…Application/Services/IntegrationOAuthStatePurgeJobHandler.cs
src/…Infrastructure/Amazon/AmazonLwaClient.cs
src/…Infrastructure/Amazon/AmazonLwaOptions.cs
src/…Persistence/Repositories/IntegrationOAuthStateRepository.cs
src/…API/Controllers/V1/IntegrationCallbackController.cs
tests/…DatabaseTests/IntegrationAuthorizationTests.cs
tests/…UnitTests/IntegrationAuthorizationUnitTests.cs
tests/…UnitTests/AmazonLwaClientTests.cs
```

### Migration

`20261106205737_AddIntegrationOAuthStateAndProviderSeed`

- One `CreateTable` (`IntegrationOAuthStates`), three indexes (unique on `TokenHash`, one on
  `ExpiresAt`, the FK index).
- Two `InsertData` rows seeding `IntegrationProviders`: `AMAZON_SP` enabled, `AMAZON_BUSINESS`
  **disabled** pending its 4–6 week onboarding.
- **Zero** `AddColumn` / `AlterColumn` / `DropColumn`. No existing table is touched, so the
  migration cannot affect SPR or M360 behaviour.

One line of the generated migration was edited by hand: EF omitted `IsEnabled` from the
`AMAZON_BUSINESS` insert because `false` is the CLR default, and the **column** default is
`true` — left alone, the seed would have enabled Amazon Business everywhere. The column is now
listed explicitly.

---

## 2. Security decisions worth recording

**The state token is the entire boundary.** The callback is anonymous — it has to be, a browser
arrives carrying only what Amazon echoes back. It therefore trusts exactly one thing: that the
presented state hashes to a stored, unused, unexpired row. Organization, tenant, connection and
return destination all come from that row. No route value, query parameter or header influences
what gets attached to whom.

- 256-bit random token, **SHA-256 hash only** at rest (the discipline `OrgPortalUserToken`
  already uses), 10-minute lifetime.
- `ValidateAndConsumeAsync` is deliberately one operation. Validating and consuming separately
  leaves a window in which two concurrent callbacks both see a valid state.
- Validation happens **before** the token exchange, so an attacker cannot burn authorization
  codes without holding a valid state.
- The route's `providerCode` is checked against the connection's provider: a state cannot be
  redeemed through a different provider's door.
- Every callback failure returns one identical message. Distinguishing "unknown" from "expired"
  would tell a prober which guesses were close.
- Return URLs are checked against an exact scheme/host/port/path-prefix allow-list **both** when
  the state is issued and again at redirect time. An empty allow-list honours nothing and renders
  a plain result page — that is the safe default, not a misconfiguration.
- Rate limited with the existing `public-auth` policy (5/min per client IP).

**Tokens.** Refresh tokens go through `IIntegrationCredentialStore` (AES-GCM, strict decrypt).
Access tokens are never persisted — in-memory only, keyed by **connection id and nothing else**;
a cache keyed by region or provider would hand one seller's token to another seller's request.
`AmazonLwaClient` logs Amazon's error *codes* but never a request body, a code, or a token.

**Health.** A refresh rejected with `invalid_grant` / `invalid_client` / `unauthorized_client`
moves the connection to `Expired` — it will keep being rejected, and a poller that retries it
forever burns rate-limit budget belonging to connections that work. Anything else is recorded as
a transient error with the status untouched, so an Amazon outage does not disconnect every
healthy seller. A `Revoked` connection is never promoted to `Expired` by a late in-flight call.

---

## 3. Test results

| Suite | Result |
|---|---|
| Unit | **225 passed**, 0 failed |
| Database (real PostgreSQL via Testcontainers) | **88 passed**, 0 failed |
| Integration | **11 passed**, 0 failed |

All pre-existing SPR and M360 tests remain green.

### Matrix coverage

| # | Requirement | Test |
|---|---|---|
| 2.1 | State is single-use | `A_state_cannot_be_used_twice` — the replay runs through a **fresh DbContext**, so what rejects it is the persisted `UsedAt`, not a tracked entity |
| 2.2 | State expiry | `An_expired_state_is_rejected` |
| 2.3 | State tampering | `A_tampered_state_token_attaches_nothing` |
| 2.4 | **Cross-tenant attach** | `A_states_tenant_decides_the_connection_not_the_callback`, `A_state_whose_connection_belongs_to_another_tenant_is_refused` |
| 2.5 | Reconnect reuses the connection (C3) | `Disconnecting_then_reconnecting_reuses_the_same_connection_and_its_watermark` — asserts same id **and** preserved watermark |
| 2.6 | Revoked reconnect reactivates | same test (reconnect is from `Revoked`) + `Re_consenting_replaces_the_token_without_creating_a_connection` |
| 2.7 | Same account, two tenants | `The_same_seller_account_may_be_connected_by_two_tenants` |
| 2.8 | Account replacement | `Authorizing_a_different_seller_account_creates_a_new_connection` |
| 2.9 | Revocation stops work | `A_revoked_connection_yields_no_access_token`, `Another_tenant_cannot_obtain_a_token_for_this_connection`, `A_rejected_credential_on_a_revoked_connection_does_not_resurrect_it` |
| 2.10 | Refresh token never logged | `No_log_written_during_a_full_flow_contains_token_material` — a recording logger is injected into every service in the flow and the captured output is asserted free of the refresh token, the `Atzr|` prefix, the authorization code and the state |

Also covered: the return-URL allow-list (12 cases including the suffix trick
`app.merchant360.com.evil.com`, scheme downgrade, port change and `javascript:`), the token cache
(including a concurrency test proving 8 simultaneous callers refresh once), and the LWA client
against a stubbed transport (success, `invalid_grant`, non-JSON error body, HTTP 200 with no
token, unconfigured, unreachable).

---

## 4. Contract conformance

Routes now match `partnerconnect-amazon-api-v1.openapi.yaml` v1.1:

- `POST /api/v1/org/tenants/{tenantId}/integrations/{providerCode}/authorize` → `201` with
  `{ integrationConnectionId, consentUrl, stateExpiresAt }`
- `GET /api/v1/integrations/callback/{providerCode}` → anonymous, state-validated, rate-limited

**Two documented divergences**, neither requiring a contract change in my view, but both flagged
for the next revision:

1. `POST /api/v1/org/tenants/{tenantId}/integrations` (begin without authorizing) exists in code
   from Phase 1 but is not in the contract. It is retained because the substrate supports
   providers whose credentials arrive by API key or SFTP, where there is a connection to
   configure but no consent to obtain. It is not used by the Amazon flow.
2. The contract documents only `302` for the callback. The implementation returns a plain result
   page when no allow-listed return URL is bound — there is nowhere to redirect to, and inventing
   one would defeat the allow-list.

---

## 5. Gates still open

**Phase 2 cannot be *claimed* complete** until these clear. Nothing below has been executed.

| # | Gate | Owner | Blocks |
|---|---|---|---|
| G1 | Amazon developer registration / SP-API application approved | Product owner | a real seller connecting |
| G2 | `Amazon:Lwa` configured (ApplicationId, ClientId, ClientSecret, RedirectUri) in protected configuration | Ops | authorize returns 422 until then, by design |
| G3 | `CredentialEncryption__EncryptionKey` installed on the droplet | **Ops — still open from Phase 1** | storing a real refresh token |
| G4 | The redirect URI registered on the Amazon application must match `RedirectUri` exactly | Ops | token exchange |
| G5 | `Integrations:Authorization:AllowedReturnUrls` populated with the M360 origin | Ops | returning the user to M360 |

G3 is the Phase 1 item that has not moved: the droplet's `/etc/merchant360/env/pc-api.env` (and `pc-workers.env`) still contain no
`CredentialEncryption__EncryptionKey`, so the API is running on the published development
fallback key. **Phase 2 development is unaffected** — no real token has been written anywhere —
but no production refresh token may be stored until that key is installed, which remains gated on
verified encrypted backups per the approved sequencing.

### A configuration gap found after this report was first written — now fixed

`AddPartnerConnectServices` took an optional `IConfiguration` with a null fallback that
registered empty options, and **both** hosts called it without one. The first version of this
report described this as a workers-only issue to defer to Phase 3. That was wrong, and the
correction matters: the API took the same path, so `Amazon:Lwa` and
`Integrations:Authorization` — the two sections this phase introduced — would have bound empty
in the API as well. Phase 2's configuration was not merely incomplete in one host; it was dead
everywhere.

Fixed in `35482e6` as a separate commit on `main`. The parameter is now required, the null
branch is gone, and `ServiceConfigurationBindingTests` asserts the options actually arrive from
the host's configuration.

Verified before making the change that no existing behaviour could shift: of the five sections
bound there, only `Email` appears in any appsettings file (the API's, where it was already being
bound identically on the following line), and none of the five appears as an environment
variable in either droplet env file. G2 and G5 below are therefore now genuinely satisfiable by
configuration alone.

---

## 6. Unchanged by this phase

No existing table, column, index or route was modified. `AddPartnerConnectServices` gained
registrations only. SPR ingestion, the M360 push, the outbox, webhook delivery and the document
state machine are untouched, and their tests pass unchanged.
