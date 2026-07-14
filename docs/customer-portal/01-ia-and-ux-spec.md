# Customer Portal — Information Architecture & UX Spec (MVP)

> Companion: [02 — Org API Contracts](02-org-api-contracts.md). No implementation yet.

## 1. Goals & principles

- **Self-service first.** Let orgs/tenants request connections, configure transport, and
  monitor feeds/orders themselves — offloading work PC operators do today.
- **Transparency over magic.** For middleware, trust comes from visible status, timestamps,
  record counts, and plain-language errors — everywhere.
- **Tenant-scoped by default.** Every operational screen is "for the selected tenant." Org
  admins get an org roll-up and a tenant switcher.
- **Build on the existing org API.** `OrgController` (`/api/v1/org`, `X-Api-Key` org-portal
  auth, association-gating). No admin endpoints exposed to customers.

## 2. Audience & assumptions

- **Organization** = the customer account (a company / reseller / dealer group).
- **Tenant** = a merchant/store under the org (maps to an M360 tenant where applicable).
- **User** = belongs to an org, scoped to some or all tenants, with a role.
- Assumes org-portal API-key auth exists (it does). **Open:** whether end users authenticate
  natively or via **SSO from M360** — see README open decisions. The auth screens below are
  written for native sign-in and would be replaced by an SSO handshake if that's the answer.

## 3. Design system — Fluent 2 / Microsoft 365 productivity

The UI follows the **Fluent Design System (Fluent 2)** and **Microsoft 365 productivity app**
conventions, implemented with the official **Fluent UI Blazor** library
(`Microsoft.FluentUI.AspNetCore.Components`). This keeps it consistent with a familiar,
accessible, enterprise productivity feel.

### 3.1 App shell (the M365 pattern)
- **Header (app bar):** product wordmark, **org switcher**, **tenant switcher**, global
  search (later), notifications (`FluentBadge` on a bell), **persona menu** (`FluentPersona`
  → profile, sign out). Fluent header height/tokens.
- **Navigation rail** (`FluentNavMenu`, collapsible to icons): the primary nav (section 5).
  Grouped, with the **Organization** group gated by role.
- **Content region:** page title + **command bar** (page-level actions) + body.

### 3.2 Core patterns
- **List + detail (master-detail):** list screens use `FluentDataGrid` (sortable, filterable,
  paged, selectable); opening a row navigates to a detail route (not an inline expander) so
  deep links and RBAC checks are clean.
- **Command bar:** primary actions (Request connection, Save, Refresh) sit in a Fluent command
  bar above the grid/detail, not scattered inline.
- **Edit in a side panel / dialog:** configuration and forms open in a `FluentDialog` or a
  right-hand `FluentPanel` (flyout) — the M365 "edit without leaving context" pattern.
- **Status via `FluentBadge`:** Pending / Active / Suspended / Error, with the Fluent color
  ramp (neutral / success / warning / danger).
- **Notices via `FluentMessageBar`:** errors, warnings, and success confirmations as message
  bars, never raw exceptions.
- **Dashboard tiles:** `FluentCard` tiles for health/summary, with the Fluent type ramp.
- **Loading & empty states:** `FluentSkeleton` while loading; explicit empty states with a
  primary action ("No connections yet → Request a connection").
- **Detail tabs:** `FluentTabs` (e.g. a connection's Status / Configuration / Activity).
- **Breadcrumbs:** `FluentBreadcrumb` for tenant → area → item.

### 3.3 Foundations
- **Theme & tokens:** Fluent design tokens for color, the **type ramp**, spacing, and
  **density** (comfortable default, compact option for data-heavy grids). Light/dark support
  and a configurable accent color (org branding later).
- **Accessibility:** target **WCAG 2.1 AA** — Fluent UI Blazor components are keyboard- and
  screen-reader-accessible by default; preserve focus order, labels, and contrast.
- **Responsive:** rail collapses to a hamburger on narrow viewports; grids reflow to
  card lists on mobile.

## 4. App shell (wireframe)

```
┌──────────────────────────────────────────────────────────────────────────┐
│  ▣ PartnerConnect   Org: Acme Group ▾   Tenant: Alpha Dealer ▾    🔔  (AR)▾ │  header / app bar
├───────────────┬──────────────────────────────────────────────────────────┤
│  ⌂ Dashboard   │  Connections                          [ + Request ] [ ↻ ] │  command bar
│  ⇄ Connections │  ┌───────────────────────────────────────────────────┐   │
│  ▤ Catalog     │  │ Partner    Status     Last activity     Actions   │   │  FluentDataGrid
│  ▦ Orders      │  │ SPR        ● Active    2h ago            View ▸    │   │
│  ◷ Activity    │  │ Etilize    ◐ Pending   —                 View ▸    │   │
│  ───────────   │  └───────────────────────────────────────────────────┘   │
│  ORGANIZATION  │                                                          │
│  ⌂ Tenants     │                                                          │
│  ⛁ Users       │                                                          │
│  ⚙ Settings    │                                                          │
└───────────────┴──────────────────────────────────────────────────────────┘
```

## 5. Navigation map

```
(pre-auth)
  Sign in · Accept invitation → set password · Forgot password
  First-run Onboarding wizard (connect first partner → credentials → DCs → test)

(tenant-scoped — follows the tenant switcher)
  Dashboard
  Connections
    Connections list · Add (Partner directory → Request) · Connection detail (Status/Config/Activity)
  Catalog
    Prices · Inventory · Content
  Orders
    Orders list · Order detail (PO → ACK → ASN → Invoice)
  Activity & Errors

(org-scoped — org-admin)
  Organization
    Tenants · Users · Settings
  Account (self, via persona menu)
```

## 6. Screen specs (MVP)

Each screen names its Fluent building blocks and the API it reads (see doc 02).

### Dashboard  `/t/{tenantId}/dashboard`
- **Purpose:** "is everything flowing?" for the tenant.
- **Layout:** grid of `FluentCard` tiles — connection health (per partner), last price /
  inventory / content sync (timestamp + record count), recent orders (mini list), open-errors
  count. `FluentMessageBar` at top for any active alert.
- **Actions (command bar):** Request connection · Refresh.
- **API:** dashboard summary (`GET /org/tenants/{id}/summary`) or composed from the reads below.

### Connections list  `/t/{tenantId}/connections`
- **FluentDataGrid:** Partner · Status (`FluentBadge`) · Last activity · Actions (View).
- **Command bar:** + Request connection → Partner directory.
- **API:** `GET /org/connections?tenantId=`.

### Partner directory + Request  `/t/{tenantId}/connections/new`
- Cards of available partners (capabilities); "Request" opens a `FluentDialog` form.
- **API:** `GET /org/partners`; `POST /org/connections`.

### Connection detail  `/t/{tenantId}/connections/{code}`
- **FluentTabs:** **Status** (approval state, health, last sync) · **Configuration** (account #,
  transport/FTP creds in a `FluentPanel` form, **DC selection**, prefs) · **Activity** (this
  connection's events). Save/Suspend/Disconnect in the command bar.
- **API:** `GET/PUT /org/connections/{code}`; `GET /org/partners/{code}/distribution-centers` (exists).

### Catalog — Prices  `/t/{tenantId}/catalog/prices`
- Search box + `FluentDataGrid` (SKU, description, cost, list, last-pushed); row → price history.
- **API:** `GET /org/tenants/{id}/prices` (+ `.../prices/{sku}/history`).

### Catalog — Inventory  `/t/{tenantId}/catalog/inventory`
- Stock by DC grid + a **SKU lookup** tool (stock/price check) using the existing services.
- **API:** `GET /org/tenants/{id}/inventory`; `POST /org/stock-check` (exists).

### Catalog — Content  `/t/{tenantId}/catalog/content`
- Coverage summary card + per-SKU availability grid + subscription state (view only in MVP).
- **API:** `GET /org/tenants/{id}/content`.

### Orders list  `/t/{tenantId}/orders`
- `FluentDataGrid`: PO# · partner · date · chain status (`FluentBadge`) · amount.
- **API:** `GET /org/tenants/{id}/orders`.

### Order detail  `/t/{tenantId}/orders/{id}`
- **Lifecycle chain** (PO → ACK → ASN → Invoice) as a horizontal stepper, line items grid,
  document viewer, exceptions in a `FluentMessageBar`.
- **API:** `GET /org/tenants/{id}/orders/{id}`.

### Activity & Errors  `/t/{tenantId}/activity`
- Filterable event feed (`FluentDataGrid` + filter chips): feeds, order events, connection
  events; errors carry plain-language reasons.
- **API:** `GET /org/tenants/{id}/activity`.

### Organization → Tenants / Users / Settings  `/org/...` (org-admin)
- **Tenants:** grid of tenants, status, M360 mapping, Add.
- **Users:** grid, roles, Invite (`FluentDialog`), deactivate.
- **Settings:** org profile + notification preferences form.
- **API:** `GET/POST /org/tenants`, `GET/POST/PUT /org/users`, `GET/PUT /org/settings`.

### Account  `/account`
- Profile, password (if native auth), notification prefs. **API:** `GET/PUT /org/me`.

## 7. Roles & visibility

| Capability | Org Admin | Tenant Manager | Viewer |
|---|:--:|:--:|:--:|
| Dashboard / Catalog / Orders / Activity (assigned tenants) | ✅ | ✅ | ✅ (read) |
| Request / configure / suspend connections | ✅ | ✅ | — |
| Organization section (Tenants / Users / Settings) | ✅ | — | — |
| Switch across **all** tenants | ✅ | assigned only | assigned only |

Built on the existing user/role/permission model; the Organization nav group is hidden for
non-admins, and write actions are disabled (not just hidden) for Viewers.

## 8. Primary flows

```
Onboarding:   Sign in → Wizard: pick partner → creds + DCs → test → Dashboard
Connect:      Connections → Request → Directory → Submit → (approval) → Configure → Active
Check status: Dashboard tile → Prices/Inventory/Content (read) OR Activity (errors)
Track order:  Orders → open → PO→ACK→ASN→Invoice chain → document viewer
Admin:        Organization → Users (invite, assign tenants, set role)
```

## 9. Routes

Tenant-in-URL (`/t/{tenantId}/...`) for clean deep links + RBAC; org-level under `/org/...`.
See doc 02 for the API each route calls.

## 10. Phasing

- **MVP (this doc):** shell + tenant switcher, connections (request/configure/status),
  dashboard, catalog **reads**, order **tracking**, activity/errors, org users/tenants.
- **Phase 2:** submit POs, live stock/price/freight lookups, content subscription management,
  notifications (email/webhook).
- **Phase 3:** webhooks + API keys / dev portal, billing & usage, self-service onboarding
  wizard polish, analytics.

## 11. Notes / risks

- **Auth depth hinges on the M360 relationship** (README decision #1). If customers are M360
  tenants, prefer **SSO from M360** over native sign-up — this replaces the auth screens with
  a handshake and simplifies the user model.
- **Reuse, don't fork:** the customer portal is a separate Blazor app but shares DTOs/clients
  with the existing org API; do **not** point it at admin endpoints.
- **"Microsoft Productivity framework" interpretation:** taken here as **Fluent 2 / M365 +
  Fluent UI Blazor**. If a different framework was intended (e.g. a specific M365 app layout,
  Power Apps, or a corporate design kit), the shell/patterns in §3–4 would be re-mapped
  accordingly — the IA (§5–9) stays the same.
