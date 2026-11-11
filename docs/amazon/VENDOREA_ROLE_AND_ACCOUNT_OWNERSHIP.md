# Vendorea's Role and Seller-Account Ownership — Binding Invariants

**Status:** binding. These are not preferences to be traded against convenience; they are the
boundary of what the platform is. Code that would violate one is wrong even if it works.

---

## 1. What Vendorea is

Vendorea is a **software and integration provider**. Each Merchant360 dealer or wholesaler
connects and transacts through **its own authorized Amazon Seller or Amazon Business account**.

Vendorea is therefore **never**:

- the merchant of record on a marketplace sale,
- the purchasing entity on an Amazon Business order,
- an intermediary receiving or holding a dealer's Amazon sales proceeds.

### How the code enforces it

There is no row anywhere in the schema through which Vendorea could hold a seller's Amazon access
on its own behalf.

| Mechanism | Where |
|---|---|
| Credentials hang off a connection, which hangs off exactly one non-nullable tenant | `IntegrationCredential.IntegrationConnectionId`, `IntegrationConnection.TenantId` |
| No provider-level or platform-level credential exists | `IntegrationProvider` has no credential field; `AmazonLwaOptions` holds only the **application** identity (client id/secret), never a refresh token |
| A credential with no owning connection is rejected by the database | `A_credential_cannot_exist_without_a_connection_to_own_it` |
| Money is recorded against the dealer's order, never aggregated to a platform account | `SalesOrder.OrderTotalAmount`, `SalesOrderLine.ProceedsRawJson` — read and reported, never re-routed |

The LWA application identity is Vendorea's because the *software* is Vendorea's. The seller grant
behind every call belongs to the dealer, and PartnerConnect never calls Amazon except as a
specific connection belonging to a specific tenant.

## 2. One seller account, one tenant

**Default and enforced:** a seller account maps to exactly one designated Merchant360 tenant.

**Allowed freely:** a tenant may hold several Amazon accounts. A dealer selling under more than
one is ordinary.

**Requires an explicit decision:** one seller account reachable by more than one tenant.

This is a schema invariant, not a service-layer habit:

```sql
CREATE UNIQUE INDEX "UX_IntegrationConnections_PrimaryAccountOwner"
  ON "IntegrationConnections" ("IntegrationProviderId", "ExternalAccountId")
  WHERE "ExternalAccountId" IS NOT NULL
    AND "AccountOwnership" = 'Primary'
    AND "Status" <> 'Revoked';
```

| Case | Behaviour |
|---|---|
| Tenant authorises an unclaimed account | Becomes its `Primary` owner |
| Tenant reauthorises an account it already holds | Same connection, same footing, watermark preserved |
| **Second tenant authorises a claimed account** | **Refused.** No connection, no credentials, audited |
| Second tenant authorises it **with an active delegation** | Allowed, recorded as `Delegated` |
| Owner disconnects, then another tenant authorises | Allowed — a release and transfer, not sharing. The first tenant's revoked row keeps its history |
| Owner's token expires | Still owned. `Expired` means "reconnect me", not "I have let go" |

**This corrected an earlier implementation.** Phase 1 and Phase 2 originally allowed two tenants
to connect the same seller account automatically, with tests asserting that as correct
(`Same_account_on_a_different_tenant_is_allowed`, matrix 2.7). Both have been inverted. The old
behaviour was silent: a seller who authorised the same account from two tenants would simply have
ended up with two sets of their orders, two inventory positions and two parties able to read
their customers' addresses — and nobody would have decided that should happen.

## 3. The delegated-access model

`IntegrationAccountDelegation` is the only route to shared access, and it is deliberately
inconvenient to create.

- **Administrator only.** `POST /api/v1/admin/integration-delegations` requires the `admin` scope.
  Not reachable with an organization API key: an integration credential must not be able to
  decide that one dealer may read another's Amazon business.
- **Attributable.** `GrantedBy` is required and non-nullable. A grant nobody is named for is an
  unexplained exception sitting in a table, not an authorization.
- **No pre-authorisation.** A grant for an account nobody owns is refused — it would be an orphan
  that became meaningful the moment somebody claimed the account.
- **Unreachable from the OAuth flow.** No callback, redirect, state token or org key can create
  one. The authorization service can only *read* delegations, never write them.
- **Revocation takes effect.** Withdrawing a grant disconnects the delegate and destroys its
  credentials. A revocation that left access working would be a record of a decision rather than
  the decision happening.
- **A delegation grants the right to connect, not a connection.** The delegate authorises the
  account itself and receives its own credentials, its own watermark and its own orders. Nothing
  is ever read across the tenant boundary.

## 4. Shared rate-limit infrastructure, isolated data

Amazon meters per application **per selling partner**, so the rate-limit bucket must be keyed on
the seller account — two tenants holding one account genuinely share one Amazon budget, and
keying per connection would let them jointly overspend it and earn 429s for both.

That is the **only** thing they share. `ProviderRateLimitBuckets` holds exactly six columns:

```
Id · BucketKey · Operation · Tokens · LastRefillAt · CreatedAt
```

No tenant id. No credential. No order. No customer field. A test asserts that column list
precisely, so a future column cannot be added without someone deciding to change it:
`The_rate_limit_bucket_holds_no_tenant_credential_or_order_data`.

The one cross-tenant read anywhere in the integration repositories is
`FindOwningTenantAsync`, which returns an **integer tenant id** and nothing else — not the
connection, not its navigations. Only the existence of a claim crosses the boundary, which is
what lets the authorization flow refuse a second tenant without ever reading the first tenant's
data.

## 5. Amazon Business purchasing — the same principle

Phase 7 is not yet built. These constraints are binding on it now, before any design work:

- Orders must be placed on the **dealer's own authorized Amazon Business buying account**.
  Vendorea is not the buyer and holds no buying account of its own.
- The dealer's **purchasing group** must be used as configured on that account. PartnerConnect
  does not select, create or default one.
- The dealer's **configured payment arrangements** apply. No Vendorea payment instrument,
  corporate card or billing account may ever be used, and none is to be stored.
- `AmazonBusinessOrderPlacement` role grants belong to the dealer's account, and the same
  one-account-one-tenant and delegation rules apply.
- Anything that would make Vendorea the purchasing entity of record — a shared buying account, a
  pooled payment method, a consolidated invoice from Amazon to Vendorea — is out of scope by
  definition, not by prioritisation.

## 6. Tests that hold these invariants

| Invariant | Test |
|---|---|
| Database refuses a second owner | `The_database_refuses_a_second_primary_owner_for_one_seller_account` |
| Service refuses, leaves no credentials, audits | `A_second_tenant_cannot_claim_an_account_the_first_tenant_owns` |
| Several accounts per tenant allowed | `A_tenant_may_hold_several_seller_accounts` |
| Delegation permits the exception | `A_delegated_tenant_may_connect_an_account_another_tenant_owns` |
| Delegate cannot become owner by reconnecting | `A_delegate_cannot_promote_itself_to_owner_by_reconnecting` |
| Grant must be attributable | `A_delegation_must_name_who_granted_it` |
| No pre-authorised grants | `A_delegation_cannot_be_granted_for_an_account_nobody_owns` |
| Revocation disconnects and destroys credentials | `Withdrawing_a_delegation_disconnects_the_delegate_and_destroys_its_credentials` |
| Expired still owns | `An_expired_connection_still_holds_its_account` |
| Release and transfer | `An_account_released_by_its_owner_may_be_claimed_by_another_tenant` |
| Shared bucket carries no tenant data | `The_rate_limit_bucket_holds_no_tenant_credential_or_order_data` |
| Credentials and orders stay separate under delegation | `Two_tenants_on_one_seller_account_keep_separate_credentials_and_orders` |
| No platform-level credential possible | `A_credential_cannot_exist_without_a_connection_to_own_it` |
| Ownership lookup leaks nothing | `Finding_an_accounts_owner_returns_a_tenant_id_and_nothing_else` |
