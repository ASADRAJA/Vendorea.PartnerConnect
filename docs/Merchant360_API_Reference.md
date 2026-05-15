# PartnerConnect ↔ Merchant360 Integration Services

## Overview: Two-Tier Sync Strategy

| Tier | Direction | Purpose |
|------|-----------|---------|
| **Tier 1** | M360 → PC | M360 pulls partner catalog (lightweight, for subscription UI) |
| **Tier 2** | PC → M360 | PC pushes data with partner metadata (for auto-upsert) |

---

## TIER 1: M360 Pulls from PartnerConnect

### Endpoint: `GET /api/v1/partners`
**Purpose**: M360 pulls the partner catalog to display available suppliers for merchant subscriptions.

**Authentication**: OAuth2 Bearer Token (or API Key)

**Response**:
```json
[
  {
    "id": 1,
    "code": "SPR",
    "name": "SPR (Sports Parts Retailer)",
    "description": "Premium sports equipment distributor",
    "logoUrl": "https://cdn.partnerconnect.com/logos/spr.png",
    "hasPriceData": true,
    "hasEnhancedContent": true,
    "isActive": true
  }
]
```

### Endpoint: `GET /api/v1/partners/{id}`
**Purpose**: Get specific partner details.

**Response**: Same structure as above (single object).

---

## TIER 2: PartnerConnect Pushes to M360

### 1. Price Updates
**PC calls M360**: `POST /api/v1/partner-connect/merchants/{merchantId}/prices/batch`

**Payload**:
```json
{
  "tradingPartner": {
    "partnerConnectId": 1,
    "code": "SPR",
    "name": "SPR (Sports Parts Retailer)",
    "description": "Premium sports equipment distributor",
    "logoUrl": "https://cdn.partnerconnect.com/logos/spr.png"
  },
  "items": [
    {
      "sku": "SPR-12345",
      "cost": 49.99,
      "listPrice": 79.99,
      "currencyCode": "USD"
    }
  ]
}
```

**M360 Expected Response**:
```json
{
  "success": true,
  "updatedCount": 150,
  "skippedCount": 2,
  "errorCount": 0,
  "errors": null
}
```

---

### 2. Inventory Updates
**PC calls M360**: `POST /api/v1/partner-connect/merchants/{merchantId}/inventory/batch`

**Payload**:
```json
{
  "tradingPartner": {
    "partnerConnectId": 1,
    "code": "SPR",
    "name": "SPR (Sports Parts Retailer)",
    "description": "Premium sports equipment distributor",
    "logoUrl": "https://cdn.partnerconnect.com/logos/spr.png"
  },
  "items": [
    {
      "sku": "SPR-12345",
      "quantityAvailable": 100,
      "quantityOnOrder": 50,
      "warehouseCode": "EAST"
    }
  ]
}
```

**M360 Expected Response**:
```json
{
  "success": true,
  "updatedCount": 150,
  "skippedCount": 0,
  "errorCount": 0,
  "errors": null
}
```

---

## Subscription Management (M360 → PC)

**PartnerConnect is the source of truth for subscription requests.** M360 calls PC to create subscription requests, and PC Admin Portal manages approval/denial.

### M360 Creates Subscription Request

**M360 calls PC**: `POST /api/v1/trading-partner-subscriptions/request`

**Payload**:
```json
{
  "tenantId": 123,
  "tradingPartnerId": 1,
  "accountNumber": "ACCT-001"
}
```

**Response** (201 Created):
```json
{
  "id": 1,
  "tenantId": 123,
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "tradingPartnerName": "S.P. Richards",
  "accountNumber": "ACCT-001",
  "status": "Pending",
  "requestedAt": "2026-05-15T10:00:00Z",
  "message": "Subscription request created successfully"
}
```

### PC Subscription Endpoints (called by M360)

| Method | PC Endpoint | Purpose |
|--------|-------------|---------|
| `POST` | `/api/v1/trading-partner-subscriptions/request` | Create subscription request |
| `GET` | `/api/v1/trading-partner-subscriptions` | List all subscription requests |
| `GET` | `/api/v1/trading-partner-subscriptions/{id}` | Get subscription request details |
| `POST` | `/api/v1/trading-partner-subscriptions/{id}/approve` | Approve subscription |
| `POST` | `/api/v1/trading-partner-subscriptions/{id}/deny` | Deny subscription |

### Subscription Status Flow

```
M360 Request → [Pending] → PC Admin Approves → [Approved] → PC pushes data to M360
                         → PC Admin Denies  → [Denied]

[Approved] → PC Admin Suspends → [Suspended] → PC Admin Reactivates → [Approved]
```

### Subscription Response DTO

```json
{
  "id": 1,
  "tenantId": 123,
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "tradingPartnerName": "S.P. Richards",
  "accountNumber": "ACCT-001",
  "status": "Pending",
  "requestedAt": "2026-05-15T10:00:00Z",
  "approvedAt": null,
  "deniedAt": null,
  "denialReason": null,
  "message": "Subscription request created successfully"
}
```

### Deny Request DTO

```json
{
  "reason": "Invalid account number",
  "notes": "Optional notes"
}
```

---

## Data Models

### TradingPartnerInfo (included in all push payloads)
```csharp
record TradingPartnerInfo(
    int PartnerConnectId,   // PC's internal ID
    string Code,            // e.g., "SPR"
    string Name,            // Display name
    string? Description,    // Optional description
    string? LogoUrl         // Optional branding
);
```

### Subscription Status Enum
```
Pending | Approved | Denied | Suspended
```

---

## Authentication

| Direction | Auth Method |
|-----------|-------------|
| M360 → PC | OAuth2 Bearer Token (client credentials) |
| PC → M360 | OAuth2 Bearer Token (client credentials) |

PC has `OAuth2TokenHandler` that automatically handles token refresh.

---

## Call Flow Diagram

```
┌─────────────────┐                    ┌─────────────────┐
│   Merchant360   │                    │  PartnerConnect │
└────────┬────────┘                    └────────┬────────┘
         │                                      │
         │  TIER 1: Pull Partner Catalog        │
         │─────────────────────────────────────>│
         │  GET /api/v1/partners                │
         │<─────────────────────────────────────│
         │  [partners with hasPriceData, etc.]  │
         │                                      │
         │  Create Subscription Request         │
         │─────────────────────────────────────>│
         │  POST /api/v1/trading-partner-       │
         │       subscriptions/request          │
         │<─────────────────────────────────────│
         │  { id: 1, status: "Pending" }        │
         │                                      │
         │        (PC Admin approves)           │
         │                                      │
         │  TIER 2: Receive Data Pushes         │
         │<─────────────────────────────────────│
         │  POST .../prices/batch               │
         │  (includes tradingPartner block)     │
         │─────────────────────────────────────>│
         │  { success: true, ... }              │
         │                                      │
```

---

## Key Implementation Notes for M360 Developer

1. **Partner Upsert**: When receiving price/inventory pushes, M360 should upsert the `tradingPartner` block to maintain a local partner record.

2. **Data Availability Flags**: The `hasPriceData` and `hasEnhancedContent` flags indicate whether PC has data loaded for that supplier - useful for UI display.

3. **OAuth2 Tokens**: Both sides use client credentials flow. PC's token handler auto-refreshes.

4. **Subscription Workflow**:
   - Merchant requests subscription in M360 UI
   - M360 calls PC: `POST /api/v1/trading-partner-subscriptions/request`
   - PC stores request with status "Pending" (PC is source of truth)
   - PC admin approves/denies via Admin Portal
   - On approval, PC starts pushing data for that merchant/partner combo
   - M360 can poll PC for subscription status or wait for webhook notification (TODO)

5. **Subscription Request Endpoint**:
   - URL: `POST /api/v1/trading-partner-subscriptions/request`
   - Required fields: `tenantId`, `tradingPartnerId`, `accountNumber`
   - Returns 201 with subscription details on success
   - Returns 200 if subscription already exists (idempotent)
