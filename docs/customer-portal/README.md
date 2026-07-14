# PartnerConnect Customer Portal

Design docs for the **customer-facing** portal (for registered organizations and their
tenants) — distinct from the internal Admin Portal that PC operators use.

> Status: **design / MVP planning**. No implementation yet.

## Contents

| Doc | What it covers |
|---|---|
| [01 — IA & UX Spec](01-ia-and-ux-spec.md) | Audience, design system (Fluent 2 / Microsoft 365), information architecture (nav, screens, flows, roles, routes), screen specs, phasing |
| [02 — Org API Contracts](02-org-api-contracts.md) | New `/api/v1/org` endpoints the MVP needs, request/response DTOs, auth & tenant scoping, error model |

## One-paragraph summary

A **self-service** portal so organizations and their tenants can do what PC operators do
today: request and configure partner connections, and monitor the data that flows through
PC (prices, inventory, content, orders). Built in **Blazor** with **Fluent UI Blazor**
components following **Fluent 2 / Microsoft 365 productivity** patterns, on top of the
existing org-facing API (`OrgController`, `X-Api-Key` org-portal auth, association-gating).

## Open decisions (settle before building)

1. **Audience & relationship to Merchant360.** Partner-connect requests today *originate
   from M360*, yet PC also supports external orgs (`ExternalDealerRepository`). Is this
   portal for **external orgs using PC independently**, for **M360 merchants** to manage
   partner-specific things M360 doesn't, or **both**? This decides whether the portal
   replaces, complements, or sits behind M360 — and whether auth is native sign-up or
   **SSO from M360**.
2. **Org ↔ tenant ↔ user model** and who self-registers.
3. **Read-only vs actionable** depth for the first cut (a "status portal" is far cheaper).
