# Merchant360 → PartnerConnect: Canonical Order Submission Contract

**Date:** 2026-06-06
**Build Status:** 0 Errors, 82 Tests Passed

---

## Overview

This document defines the canonical API contract for order submission from Merchant360 (and other authorized platforms) to PartnerConnect. The contract is **partner-agnostic** - M360 submits business intent, not EDI/XML details.

### Key Principles

1. **M360 sends business intent, not EDI/XML details**
2. **PartnerConnect resolves partner-specific execution internally**
3. **No SPR-specific fields exposed in the M360-facing contract**
4. **Supports many organizations, many merchants, many partners**

---

## API Endpoint

```
POST /api/integrations/orders
```

### Authentication

- Service-to-service authentication (OAuth2 client credentials)
- Required scopes: `partnerconnect.orders.write`

### Idempotency

- **Required**: `idempotencyKey` in request body
- Duplicate submissions return existing order (200 OK, `isDuplicate: true`)
- Conflicting submissions (same key, different content) return 409 Conflict

---

## Request Schema

```json
{
  // === ROUTING CONTEXT (Required) ===
  "sourcePlatform": "Merchant360",
  "organizationId": 1,
  "merchantId": 100,
  "partnerConnectionId": 50,
  "externalOrderId": "M360-ORDER-12345",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "idempotencyKey": "M360-2024-06-06-ORDER-12345",
  "submittedBy": "merchant-user@example.com",

  // === ORDER HEADER ===
  "poNumber": "PO-2024-001",
  "orderDate": "2024-06-06T10:00:00Z",
  "notes": "Deliver before noon",
  "externalReferences": "{\"departmentCode\": \"ACCT\"}",

  // === LABEL FIELDS (customer-facing shipping label) ===
  "attn": "Receiving Dept",                       // → SPR DealerAttn (max 25)
  "labelComments": ["Handle with care", "Fragile"], // → SPR LabelCmmnts1..3 (max 25 each, up to 3)

  // === SHIP-TO ADDRESS (the end customer) ===
  "shipTo": {
    "name": "John Smith",
    "company": "Acme Corp",
    "address1": "123 Main Street",
    "address2": "Suite 100",
    "address3": "Building C",
    "city": "Chicago",
    "state": "IL",
    "postalCode": "60601",
    "country": "US",
    "phone": "312-555-1234",
    "email": "john@acme.com",
    "isResidential": false                         // false → IsCommercialAddress=Y; true → N
  },

  // === SHIP-FROM ADDRESS (the merchant business shown on the label) ===
  // Optional. Logo/phone/website are NOT sent — SPR uses the dealer's stored label profile.
  "shipFrom": {
    "company": "Acme Merchant LLC",
    "address1": "500 Dealer Rd",
    "city": "Atlanta",
    "state": "GA",
    "postalCode": "30339",
    "country": "US"
  },

  // === BILL-TO ADDRESS (Optional) ===
  "billTo": null,

  // === ORDER LINES ===
  "lines": [
    {
      "lineNumber": 1,
      "vendorSku": "SPR-ABC-123",
      "buyerSku": "MY-SKU-001",
      "upc": "012345678901",
      "quantity": 10,
      "unitOfMeasure": "EA",
      "unitPrice": 24.99,
      "description": "Office Chair",
      "requestedDeliveryDate": "2024-06-10",
      "notes": null,
      "externalLineReference": "LINE-001"
    }
  ],

  // === BUSINESS OPTIONS ===
  "orderType": "WrapAndLabel",                     // "StockOrder"|"WrapAndLabel"|"DropShip"; default WrapAndLabel
  "distributionCenterCode": "8",                   // ship-from SPR DC → Order/@ShipNode (optional)
  "allowPartialShipment": true,
  "allowBackorder": true,
  "allowSubstitutions": false,
  "fulfillmentPreference": "Standard",
  "requestedShipDate": "2024-06-07",
  "requestedDeliveryDate": "2024-06-10",
  "shippingMethod": "Ground"
}
```

---

## Response Schema

### Success (202 Accepted)

```json
{
  "accepted": true,
  "partnerConnectOrderId": 12345,
  "partnerDocumentId": null,
  "status": "Accepted",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "acceptedAt": "2024-06-06T10:00:05Z",
  "warnings": [
    "SPR EnterpriseCode not configured; using default"
  ],
  "errors": [],
  "isDuplicate": false
}
```

### Duplicate (200 OK)

```json
{
  "accepted": true,
  "partnerConnectOrderId": 12345,
  "status": "Duplicate",
  "correlationId": "...",
  "isDuplicate": true
}
```

### Validation Failed (400 Bad Request)

```json
{
  "accepted": false,
  "status": "ValidationFailed",
  "correlationId": "...",
  "errors": [
    { "code": "REQUIRED_FIELD", "field": "PoNumber", "message": "Purchase order number is required" },
    { "code": "SPR_REQUIRES_CITY", "field": "ShipTo.City", "message": "SPR orders require ship-to city" }
  ]
}
```

### Conflict (409 Conflict)

```json
{
  "accepted": false,
  "partnerConnectOrderId": 12345,
  "status": "Conflict",
  "correlationId": "...",
  "errors": [
    { "code": "IDEMPOTENCY_CONFLICT", "field": "IdempotencyKey", "message": "Order already exists with different content" }
  ]
}
```

---

## Field Responsibility Mapping

### Required from Merchant360

| Field | Type | Description |
|-------|------|-------------|
| `sourcePlatform` | string | Identifies calling system (e.g., "Merchant360") |
| `organizationId` | int | PartnerConnect organization ID |
| `merchantId` | int | Merchant/tenant ID in PartnerConnect |
| `partnerConnectionId` | int | Partner connection ID linking merchant to partner |
| `externalOrderId` | string | M360's order ID for correlation |
| `correlationId` | string | Distributed tracing ID |
| `idempotencyKey` | string | Unique key for duplicate detection |
| `poNumber` | string | Customer's purchase order number |
| `shipTo.address1` | string | Ship-to address line 1 |
| `shipTo.city` | string | Ship-to city |
| `shipTo.state` | string | Ship-to state/province |
| `shipTo.postalCode` | string | Ship-to postal code |
| `lines[].vendorSku` | string | Partner's SKU for each line |
| `lines[].quantity` | decimal | Quantity ordered (must be > 0) |

### Required from PartnerConnect Configuration

| Field | Source | Description |
|-------|--------|-------------|
| `enterpriseCode` | TenantPartnerAccount.ConfigurationJson | SPR enterprise code |
| `buyerOrgCode` | TenantPartnerAccount.ConfigurationJson or AccountNumber | SPR buyer organization code |
| `sellerOrgCode` | TenantPartnerAccount.ConfigurationJson or TradingPartner.Code | SPR seller organization code |
| `shipNode` | TenantPartnerAccount.ConfigurationJson | SPR ship node (if required) |
| `transportType` | TenantPartnerAccount.ConfigurationJson | SFTP/AS2/HTTP |
| `outboundPath` | TenantPartnerAccount.ConfigurationJson | EDI outbound path |
| `autoSend997` | TenantPartnerAccount.ConfigurationJson | Auto-acknowledge setting |

### Derived Internally by PartnerConnect

| Field | Derivation | Description |
|-------|------------|-------------|
| `requireCompleteShipment` | `!allowPartialShipment` | SPR IsShipComplete flag |
| `orderDate` | Current UTC if not provided | When order was placed |
| `correlationId` | Generated GUID if not provided | Request tracking |
| `lineNumber` | Auto-assigned if not provided | Sequential line numbering |
| `status` | Always "Submitted" | Initial order status |
| `totalAmount` | Sum of line totals | Calculated order total |

### Optional

| Field | Type | Description |
|-------|------|-------------|
| `submittedBy` | string | Who submitted (username, service account) |
| `orderDate` | datetime | When the order was placed |
| `notes` | string | Order-level notes/instructions |
| `externalReferences` | string (JSON) | Additional external identifiers |
| `orderType` | string | `StockOrder`\|`WrapAndLabel`\|`DropShip`. **Defaults to `WrapAndLabel`** (SPR 03) when omitted |
| `distributionCenterCode` | string | Ship-from SPR DC code → `Order/@ShipNode` (SPR selects a DC when omitted) |
| `attn` | string | Attention line for the label → SPR `DealerAttn` (max 25) |
| `labelComments` | string[] | Up to 3 dealer comment lines for the label → SPR `LabelCmmnts1..3` (max 25 each) |
| `shipFrom` | object | Merchant business shown as the label ship-from → `PersonInfoContact`. Logo/phone/website NOT sent (SPR uses the dealer profile) |
| `shipTo.address3` | string | Third ship-to address line → `PersonInfoShipTo/@AddressLine3` |
| `shipTo.isResidential` | bool | Residential vs commercial; maps (inverted) to `PersonInfoShipTo/@IsCommercialAddress` |
| `billTo` | object | Bill-to address (defaults to ship-to) |
| `requestedShipDate` | datetime | Preferred ship date |
| `requestedDeliveryDate` | datetime | Preferred delivery date |
| `shippingMethod` | string | Preferred shipping method |
| `fulfillmentPreference` | string | Fulfillment priority |
| `lines[].buyerSku` | string | Merchant's internal SKU |
| `lines[].upc` | string | UPC/EAN barcode |
| `lines[].unitPrice` | decimal | Unit price (may use partner catalog if not provided) |
| `lines[].description` | string | Item description |
| `lines[].notes` | string | Line-level notes → SPR line-level `Note/@NoteText` |
| `lines[].externalLineReference` | string | External line identifier |

---

## SPR XML Conformance

### Downstream Generation

When an order is accepted for SPR, PartnerConnect internally:

1. Resolves `enterpriseCode`, `buyerOrgCode`, `sellerOrgCode` from partner configuration
2. Maps business options to SPR-specific flags (`IsShipComplete` from `!allowPartialShipment`)
3. Generates EZPO4 XML using `SprEzpo4Generator`
4. Validates generated XML against EZPO4 XSD (if available)
5. Transmits via configured transport (SFTP/AS2)

### Required Canonical → SPR Mappings

| Canonical Field | SPR XML Element/Attribute |
|-----------------|---------------------------|
| `poNumber` | `Order/@CustomerPONo` (SPR assigns its own `OrderNo` on the POACK) |
| `orderType` | `Order/@OrderType` — `StockOrder`→`01`, `WrapAndLabel`→`03`, `DropShip`→`04`; default `03` |
| `distributionCenterCode` | `Order/@ShipNode` |
| `shipTo.name`/`company` | `PersonInfoShipTo/@FirstName` |
| `shipTo.address1` | `PersonInfoShipTo/@AddressLine1` |
| `shipTo.address2` | `PersonInfoShipTo/@AddressLine2` |
| `shipTo.address3` | `PersonInfoShipTo/@AddressLine3` |
| `shipTo.city` | `PersonInfoShipTo/@City` |
| `shipTo.state` | `PersonInfoShipTo/@State` |
| `shipTo.postalCode` | `PersonInfoShipTo/@ZipCode` |
| `shipTo.isResidential` | `PersonInfoShipTo/@IsCommercialAddress` (inverted: residential→`N`, commercial→`Y`) |
| `shipFrom.company`/address | `PersonInfoContact/@FirstName` + address attributes |
| `attn` | `EXTNSprOrderHeader/@DealerAttn` (+ `@AttnDesc="ATTN"`) |
| `labelComments[0..2]` | `EXTNSprOrderHeader/@LabelCmmnts1..3` |
| `lines[].vendorSku` | `OrderLine/Item/@CustomerItem` |
| `lines[].quantity` | `OrderLineTranQuantity/@OrderedQty` |
| `lines[].unitOfMeasure` | `OrderLineTranQuantity/@TransactionalUOM` |
| `lines[].notes` | `OrderLine/Notes/Note/@NoteText` |

### Validation Before Document Generation

PartnerConnect validates before generating SPR XML:

1. All required address fields are present
2. All lines have VendorSku
3. Partner configuration has required org codes
4. Generated XML passes XSD validation (when schema available)

If validation fails after acceptance, the order transitions to `Failed` status with actionable diagnostics.

---

## Files Changed

### New Files

| File | Purpose |
|------|---------|
| `Contracts/Integration/SupplierOrderIntake.cs` | Canonical request/response DTOs |
| `Application/Interfaces/ISupplierOrderIntakeService.cs` | Intake and resolution interfaces |
| `Application/Services/SupplierOrderIntakeService.cs` | Main intake orchestration |
| `Application/Services/PartnerOrderResolutionService.cs` | Partner-specific resolution |
| `API/Controllers/IntegrationOrdersController.cs` | Integration API endpoint |
| `tests/.../SupplierOrderIntakeServiceTests.cs` | 22 unit tests |

### Modified Files

| File | Change |
|------|--------|
| `Domain/Entities/Order.cs` | Added integration tracking and business options fields |
| `Persistence/Configurations/OrderConfiguration.cs` | Added new columns and indexes |
| `Persistence/Repositories/OrderRepository.cs` | Added idempotency and external ID lookups |
| `Application/Interfaces/IOrderRepository.cs` | Added new query methods |
| `Infrastructure/.../ServiceCollectionExtensions.cs` | Registered new services |

---

## Tests Added

| Test | Description |
|------|-------------|
| `SubmitOrder_MissingSourcePlatform_ReturnsValidationError` | Routing context validation |
| `SubmitOrder_InvalidOrganizationId_ReturnsValidationError` | Routing context validation |
| `SubmitOrder_InvalidMerchantId_ReturnsValidationError` | Routing context validation |
| `SubmitOrder_InvalidPartnerConnectionId_ReturnsValidationError` | Routing context validation |
| `SubmitOrder_MissingExternalOrderId_ReturnsValidationError` | Routing context validation |
| `SubmitOrder_MissingCorrelationId_ReturnsValidationError` | Routing context validation |
| `SubmitOrder_MissingIdempotencyKey_ReturnsValidationError` | Routing context validation |
| `SubmitOrder_MissingPoNumber_ReturnsValidationError` | Business field validation |
| `SubmitOrder_NoOrderLines_ReturnsValidationError` | Business field validation |
| `SubmitOrder_MissingVendorSku_ReturnsValidationError` | Business field validation |
| `SubmitOrder_ZeroQuantity_ReturnsValidationError` | Business field validation |
| `SubmitOrder_MissingShipTo_ReturnsValidationError` | Business field validation |
| `SubmitOrder_DuplicateSubmission_ReturnsExistingOrder` | Idempotency handling |
| `SubmitOrder_OrganizationNotFound_ReturnsValidationError` | Org/tenant validation |
| `SubmitOrder_OrganizationNotActive_ReturnsValidationError` | Org/tenant validation |
| `SubmitOrder_TenantNotFound_ReturnsValidationError` | Org/tenant validation |
| `SubmitOrder_TenantNotInOrganization_ReturnsValidationError` | Org/tenant validation |
| `SubmitOrder_PartnerConnectionNotFound_ReturnsValidationError` | Partner resolution |
| `SubmitOrder_ValidRequest_CreatesOrderSuccessfully` | Happy path |
| `SubmitOrder_ValidRequest_SetsIntegrationTrackingFields` | Integration tracking |
| `SubmitOrder_ValidRequest_CreatesOrderLines` | Line creation |
| `SubmitOrder_ValidRequest_RecordsStatusHistory` | Audit trail |

---

## Assumptions for M360 Side

1. **M360 must maintain mapping** of PartnerConnect organization/merchant/partner connection IDs
2. **M360 generates unique `externalOrderId`** for each order
3. **M360 generates unique `idempotencyKey`** for retry safety
4. **M360 passes `correlationId`** from its request context for tracing
5. **M360 validates its own business rules** before submission (product availability, credit, etc.)
6. **M360 handles async processing** - acceptance means queued, not completed
7. **M360 polls or receives webhooks** for order status updates

---

## Build/Test Results

```
Build: 0 Errors, 20 Warnings (existing, non-blocking)
Unit Tests: 81 Passed
Integration Tests: 1 Passed
Total: 82 Tests Passed
```

---

## Next Steps

### Remaining for Production

1. Add service-to-service authentication to `IntegrationOrdersController`
2. Add EF migration for new Order columns
3. Wire order acceptance to document generation queue
4. Add webhook notifications for order status changes
5. Add integration tests against M360 sandbox

### Deferred to Phase 2

1. Real-time order tracking API
2. Batch order submission
3. Order modification/amendment
4. Multi-partner order routing (single order → multiple partners)
