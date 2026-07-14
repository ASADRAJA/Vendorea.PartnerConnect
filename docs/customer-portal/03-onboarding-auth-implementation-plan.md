# Onboarding & Auth — Implementation Plan

Builds on **Auth‑1** (done). Covers native auth, invite/activation links, self‑service org
registration + operator approval, operator‑led onboarding, and tenant‑user provisioning.

## Locked decisions
- **Native email/password** login (done in Auth‑1).
- **Invite/activation links** — users set their own password via a secure link; **no password is ever emailed**.
- **Org onboarding: both** — public self‑service registration **with PC‑operator approval**, *and* operator‑led creation (no approval).
- **Tenant users: both** — OrgAdmin **invites** *and* a **request‑to‑join** flow the OrgAdmin approves.
- **Email:** SMTP, configurable; dev target `localhost:3000` (confirm actual SMTP port — smtp4dev/Mailhog usually 25/1025 with a web UI on 3000/5000).
- **Roles:** OrgAdmin / TenantManager / Viewer.

## Already delivered — Auth‑1 (baseline)
`OrgPortalUser` (org, email, PBKDF2 hash, role, tenant scope, lockout) + migration; JWT + "Smart"
auth scheme (Bearer→JWT, else API‑key); `POST /org/auth/login`; real `/org/me`; admin bootstrap
`POST /admin/organizations/{id}/portal-users`; portal native login + Bearer `ApiClient`.

---

## Phase 1 — Email + Invitation/Activation (foundation; everything depends on it)
**Goal:** link‑based account activation; no emailed passwords.
- **Backend**
  - `IEmailSender` + SMTP implementation (config `Email:{Host,Port,From,UseSsl}`; dev `localhost:3000`). DI‑registered; a no‑op/logging sender for tests.
  - **Activation tokens:** add to `OrgPortalUser` a `Status` (`Invited / Active / Disabled`) + a token store (`UserActivationToken`: userId, tokenHash, purpose[Activation|PasswordReset], expiresAt, usedAt). Tokens are single‑use, short‑lived.
  - Endpoints: `GET /org/auth/activation/{token}` (validate + return the invitee's email/org for the page), `POST /org/auth/activate` (token + new password → set hash, mark Active, consume token).
  - Rework user creation (admin bootstrap + all future invite paths) to create **Invited** users and **email an activation link**, never a password.
- **Frontend (portal, public):** "Set your password" page (`/activate?token=…`) → activate endpoint → redirect to login.
- **Done when:** create a user → email arrives (in dev SMTP) → link → set password → log in.

## Phase 2 — RBAC enforcement (server + UI)
**Goal:** roles actually restrict access.
- **Backend:** map role→capability (OrgAdmin = full; TenantManager = read+write within **assigned tenants**; Viewer = read‑only). Enforce on every `/org` endpoint: read scope on GETs, write scope on mutations, and **tenant‑scope check** on `/tenants/{id}/…` (404 if outside the user's scope). Source role/scope from the JWT claims.
- **Frontend:** nav + action gating — hide the Organization section for non‑admins, disable write actions for Viewer, and limit the tenant switcher to a TenantManager's assigned tenants.
- **Done when:** Viewer can't mutate, TenantManager is confined to their tenants, OrgAdmin is unrestricted — enforced by the API, not just hidden in the UI.

## Phase 3 — Self‑service org registration + operator approval
**Goal:** the public "Register" door with a PC approval gate.
- **Backend**
  - Organization `Status`: `PendingApproval / Active / Denied / Suspended` (extend existing).
  - Registration request (reuse Organization in `PendingApproval`, or an `OrgRegistrationRequest`): org name, chosen **plan**, admin name+email, business details, submittedAt, decision + reason.
  - `POST /public/org-registrations` (anonymous) → pending org + pending OrgAdmin (Invited, no token yet).
  - Admin: `GET /admin/org-registrations` (queue), `POST …/{id}/approve`, `POST …/{id}/deny`.
  - **Approve** → org `Active`, create the plan subscription, send the OrgAdmin activation link (Phase 1). **Deny** → email the applicant; no active org.
- **Frontend**
  - Portal landing: **Log in | Register**; public **Register** page (org + plan + admin email).
  - Admin Portal: **Registration Requests** queue — review, approve/deny with reason.
- **Done when:** a stranger can register → operator approves → org‑admin activation email → in.

## Phase 4 — Operator‑led org onboarding (admin portal)
**Goal:** sales‑led onboarding without the public queue.
- Admin Portal **"Register Organization"** screen: create org (immediately `Active`) + plan + OrgAdmin (Invited) + send activation link. Reuses Phase 1 invite + Phase 3 create; **skips approval** (operator is trusted).
- **Done when:** an operator can stand up a new org + its admin directly.

## Phase 5 — Tenant users: invite + request‑to‑join
**Goal:** OrgAdmin self‑service user management (the real "Users" screen).
- **Backend**
  - Invite: `POST /org/users` (OrgAdmin) → create Invited user (role + tenant scope) + activation link. Plus `resend-invite`, `deactivate/reactivate`, `PUT /org/users/{id}` (role/scope), all OrgAdmin‑only.
  - Request‑to‑join: `POST /public/access-requests` (email + org identifier) → pending; OrgAdmin `GET /org/access-requests`, approve → create Invited user + invite / deny → notify.
- **Frontend (portal, OrgAdmin):** **Users** screen (list w/ status/role/scope; invite; resend; deactivate; edit) + **Join Requests** queue (approve/deny). Assign users to existing tenants (tenants stay operator/M360‑provisioned).
- **Done when:** an OrgAdmin can invite users and approve join requests; each gets an activation link and correctly‑scoped access.

## Phase 6 — Polish & hardening
- **Password reset** (forgot → email link → set new; reuses Phase 1 tokens).
- Token/invite **expiry + resend**; lockout tuning.
- **Rate‑limiting** on all public endpoints (register, access‑request, forgot‑password) + email verification to deter abuse/spam orgs.
- **Onboarding audit** (who approved/created/invited what) + branded email templates.

---

## Sequencing & dependencies
```
Phase 1 (email + activation)  ── foundation, do first
Phase 2 (RBAC)                ── independent; do next (roles must mean something)
Phase 3 (self-serve register) ── needs Phase 1
Phase 4 (operator onboarding) ── needs Phase 1 (reuses Phase 3 create)
Phase 5 (tenant users)        ── needs Phase 1 (+ Phase 2 for scoping)
Phase 6 (reset + hardening)   ── last
```
Each phase: build clean → seed/test the flow end‑to‑end locally → commit to `feature/customer-portal`.

## Risks / watch‑items
- **Auth‑pipeline change** (the "Smart" scheme from Auth‑1) — re‑verify existing admin/org‑key/integration auth is intact before any Azure deploy.
- **Email dependency** — confirm the dev SMTP host/port; make it fully configurable; degrade gracefully if the mail server is down (queue/retry, don't block the request).
- **Public endpoints** (register, access‑request, forgot) need **rate‑limiting + the approval gates + email verification** so they can't be abused to create spam orgs/users.
- **Migrations** — each phase adds columns/tables; apply on deploy (a recurring gotcha in this repo).
