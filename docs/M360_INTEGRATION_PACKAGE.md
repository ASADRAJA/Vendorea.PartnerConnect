# Merchant360 Integration Package
# Canonical Supplier Order Submission Contract

**Version:** 1.0.0
**Date:** 2026-06-06
**Status:** Frozen for M360 Consumption

---

## Table of Contents

1. [Overview](#overview)
2. [Final Contract Summary](#final-contract-summary)
3. [API Specification](#api-specification)
4. [Request Schema](#request-schema)
5. [Response Schema](#response-schema)
6. [Required Fields by Scenario](#required-fields-by-scenario)
7. [Field Responsibility Matrix](#field-responsibility-matrix)
8. [JSON Examples](#json-examples)
9. [M360 Implementation Guide](#m360-implementation-guide)
10. [PartnerConnect Guarantees](#partnerconnect-guarantees)

---

## Overview

This document defines the canonical API contract for order submission from Merchant360 (and other authorized platforms) to PartnerConnect. The contract is **partner-agnostic** - M360 submits business intent, not EDI/XML details.

### Key Principles

1. **M360 sends business intent, not EDI/XML details**
2. **PartnerConnect resolves partner-specific execution internally**
3. **No SPR-specific fields exposed in the M360-facing contract**
4. **Supports many organizations, many merchants, many partners**

### What M360 Should Never Know About

- SPR EnterpriseCode, BuyerOrgCode, SellerOrgCode
- ShipNode assignments
- IsShipComplete, DraftOrderFlag
- EZPO4 XML structure
- SFTP/AS2 transport details
- EDI segment/element mappings

---

## Final Contract Summary

### Endpoint

```
POST /api/integrations/orders
```

### DTO Names (Frozen)

| DTO | Purpose |
|-----|---------|
| `SubmitSupplierOrderRequest` | Canonical request for order submission |
| `SubmitSupplierOrderResponse` | Response with acceptance/rejection details |
| `CanonicalAddressInfo` | Ship-to and bill-to address structure |
| `CanonicalOrderLineRequest` | Order line item structure |
| `ValidationError` | Error detail structure |

### Field Names (Frozen)

All field names use **camelCase** in JSON and **PascalCase** in C#.

---

## API Specification

### Authentication

- Service-to-service authentication (OAuth2 client credentials)
- Required scopes: `partnerconnect.orders.write`
- API Key alternative: `X-API-Key` header

### Request Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes | `application/json` |
| `Authorization` | Yes | `Bearer {token}` or `X-API-Key: {key}` |
| `X-Correlation-Id` | No | Distributed tracing ID (also in body) |

### Response Codes

| Code | Meaning |
|------|---------|
| `202 Accepted` | Order accepted for processing |
| `200 OK` | Duplicate submission (idempotent return) |
| `400 Bad Request` | Validation failed |
| `409 Conflict` | Idempotency conflict (same key, different content) |
| `401 Unauthorized` | Authentication failed |
| `403 Forbidden` | Authorization failed |

### Idempotency

- **Required**: `idempotencyKey` in request body
- **Behavior**:
  - Same key + same content = 200 OK with `isDuplicate: true`
  - Same key + different content = 409 Conflict
- **Scope**: Per organization
- **Recommended format**: `{sourcePlatform}-{date}-{orderId}` (e.g., `M360-2024-06-06-ORDER-12345`)

---

## Request Schema

### Full Request Structure

```typescript
interface SubmitSupplierOrderRequest {
  // === ROUTING CONTEXT (Required) ===
  sourcePlatform: string;        // "Merchant360"
  organizationId: number;        // PartnerConnect org ID
  merchantId: number;            // Merchant/tenant ID
  partnerConnectionId: number;   // Partner connection ID
  externalOrderId: string;       // M360's order ID
  correlationId: string;         // UUID for distributed tracing
  idempotencyKey: string;        // Unique key for retry safety
  submittedBy?: string;          // Who submitted (optional)

  // === ORDER HEADER ===
  poNumber: string;              // Customer's PO number
  orderDate?: string;            // ISO 8601 datetime
  notes?: string;                // Order notes
  externalReferences?: string;   // JSON object for additional IDs

  // === ADDRESSES ===
  shipTo: CanonicalAddressInfo;  // Required
  billTo?: CanonicalAddressInfo; // Optional (defaults to shipTo)

  // === ORDER LINES ===
  lines: CanonicalOrderLineRequest[];

  // === BUSINESS OPTIONS ===
  orderType?: string;            // "StockOrder" | "DropShip" | "WrapAndLabel"
  allowPartialShipment?: boolean; // default: true
  allowBackorder?: boolean;       // default: true
  allowSubstitutions?: boolean;   // default: false
  shippingPriority?: string;      // "Standard" | "Expedited" | "NextDay" | "Freight"
  requestedShipDate?: string;     // ISO 8601 date
  requestedDeliveryDate?: string; // ISO 8601 date
  shippingMethod?: string;        // "Ground" | "Express" | etc.
}

interface CanonicalAddressInfo {
  name?: string;          // Contact name
  company?: string;       // Company name
  address1: string;       // Required for ship-to
  address2?: string;      // Suite, unit, etc.
  city: string;           // Required for ship-to
  state: string;          // Required for ship-to (2-letter)
  postalCode: string;     // Required for ship-to
  country?: string;       // default: "US"
  phone?: string;
  email?: string;
  isResidential?: boolean;
}

interface CanonicalOrderLineRequest {
  lineNumber?: number;             // Auto-assigned if omitted
  vendorSku: string;               // Required - partner's SKU
  buyerSku?: string;               // Merchant's SKU
  upc?: string;                    // UPC/EAN barcode
  quantity: number;                // Required, must be > 0
  unitOfMeasure?: string;          // default: "EA"
  unitPrice?: number;              // Optional
  description?: string;
  requestedDeliveryDate?: string;  // Line-level override
  notes?: string;
  externalLineReference?: string;  // External line ID
}
```

---

## Response Schema

### Success Response

```typescript
interface SubmitSupplierOrderResponse {
  accepted: boolean;               // true if order accepted
  partnerConnectOrderId?: number;  // PC internal order ID
  partnerDocumentId?: number;      // EDI document ID (if created)
  status: string;                  // "Accepted" | "Duplicate" | etc.
  correlationId: string;           // Echo back for tracing
  acceptedAt?: string;             // ISO 8601 timestamp
  warnings: string[];              // Non-blocking warnings
  errors: ValidationError[];       // Validation errors
  isDuplicate: boolean;            // True if idempotent return
}

interface ValidationError {
  code: string;      // Error code (e.g., "REQUIRED_FIELD")
  field: string;     // Field path (e.g., "Lines[0].VendorSku")
  message: string;   // Human-readable message
}
```

---

## Required Fields by Scenario

### Stock Order (Default)

Ship to dealer's warehouse for inventory replenishment.

| Field | Required | Source | Notes |
|-------|----------|--------|-------|
| `sourcePlatform` | **Yes** | M360 | e.g., "Merchant360" |
| `organizationId` | **Yes** | M360 | PC org ID |
| `merchantId` | **Yes** | M360 | Tenant/dealer ID |
| `partnerConnectionId` | **Yes** | M360 | Partner account link |
| `externalOrderId` | **Yes** | M360 | M360 order ID |
| `correlationId` | **Yes** | M360 | Tracing ID |
| `idempotencyKey` | **Yes** | M360 | Retry safety |
| `poNumber` | **Yes** | M360 | Customer PO |
| `shipTo.address1` | **Yes** | M360 | Warehouse address |
| `shipTo.city` | **Yes** | M360 | |
| `shipTo.state` | **Yes** | M360 | |
| `shipTo.postalCode` | **Yes** | M360 | |
| `lines[].vendorSku` | **Yes** | M360 | Partner's item ID |
| `lines[].quantity` | **Yes** | M360 | Must be > 0 |
| `orderType` | Optional | M360 | Defaults to "StockOrder" |
| `orderDate` | Optional | PC | Defaults to UTC now |
| `allowPartialShipment` | Optional | M360 | Defaults to true |
| `allowBackorder` | Optional | M360 | Defaults to true |
| `enterpriseCode` | Resolved | PC Config | From partner config |
| `buyerOrgCode` | Resolved | PC Config | From partner config |
| `sellerOrgCode` | Resolved | PC Config | From partner config |

### Drop Ship

Ship directly to end customer (no dealer branding).

| Field | Required | Source | Notes |
|-------|----------|--------|-------|
| All Stock Order fields | **Yes** | M360 | Same as above |
| `orderType` | **Yes** | M360 | Must be "DropShip" |
| `shipTo.name` | **Yes** | M360 | End customer name |
| `shipTo.phone` | Recommended | M360 | For delivery contact |
| `shipTo.email` | Recommended | M360 | For notifications |
| `shipTo.isResidential` | Recommended | M360 | Affects shipping options |
| `allowPartialShipment` | Typically false | M360 | Customer expects complete order |

### Wrap and Label

Ship to end customer with dealer branding/packaging.

| Field | Required | Source | Notes |
|-------|----------|--------|-------|
| All Drop Ship fields | **Yes** | M360 | Same as drop ship |
| `orderType` | **Yes** | M360 | Must be "WrapAndLabel" |
| `shipTo.company` | Recommended | M360 | End customer company |
| `notes` | Recommended | M360 | Branding instructions |
| `billTo` | Recommended | M360 | Dealer's billing address |

---

## Field Responsibility Matrix

### M360 Must Send

| Field | Description |
|-------|-------------|
| `sourcePlatform` | Always "Merchant360" |
| `organizationId` | PC organization ID (provisioned during onboarding) |
| `merchantId` | PC tenant/merchant ID |
| `partnerConnectionId` | PC partner connection ID |
| `externalOrderId` | M360's internal order ID |
| `correlationId` | UUID for distributed tracing |
| `idempotencyKey` | Unique key per order attempt |
| `poNumber` | Customer's purchase order number |
| `shipTo` | Complete ship-to address |
| `lines` | At least one line with vendorSku and quantity |

### M360 Must Persist from Response

| Field | Description |
|-------|-------------|
| `partnerConnectOrderId` | Required for status queries and support |
| `correlationId` | For log correlation across systems |
| `status` | Current order status |
| `acceptedAt` | Timestamp for audit |
| `warnings` | Any warnings returned |
| `isDuplicate` | Whether this was an idempotent return |

### PartnerConnect Resolves Internally

| Field | Source | Description |
|-------|--------|-------------|
| `enterpriseCode` | TenantPartnerAccount.ConfigurationJson | SPR enterprise code |
| `buyerOrgCode` | TenantPartnerAccount.ConfigurationJson | SPR buyer org code |
| `sellerOrgCode` | TenantPartnerAccount.ConfigurationJson | SPR seller org code |
| `shipNode` | TenantPartnerAccount.ConfigurationJson | SPR ship node (if required) |
| `isShipComplete` | Derived from `!allowPartialShipment` | SPR complete shipment flag |
| `transportType` | TenantPartnerAccount.ConfigurationJson | SFTP/AS2/HTTP |
| `outboundPath` | TenantPartnerAccount.ConfigurationJson | EDI outbound path |

### PartnerConnect Derives Internally

| Field | Derivation |
|-------|------------|
| `orderDate` | Current UTC if not provided |
| `lineNumber` | Auto-assigned 1, 2, 3... if not provided |
| `status` | Always "Submitted" on acceptance |
| `totalAmount` | Sum of line totals |
| `correlationId` | Generated UUID if not provided |

---

## JSON Examples

### Example 1: Stock Order (Minimal)

```json
{
  "sourcePlatform": "Merchant360",
  "organizationId": 1,
  "merchantId": 100,
  "partnerConnectionId": 50,
  "externalOrderId": "M360-ORDER-12345",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "idempotencyKey": "M360-2024-06-06-ORDER-12345",

  "poNumber": "PO-2024-001",
  "shipTo": {
    "company": "ABC Hardware Store",
    "address1": "123 Main Street",
    "city": "Chicago",
    "state": "IL",
    "postalCode": "60601"
  },
  "lines": [
    { "vendorSku": "SPR-WIDGET-001", "quantity": 10 },
    { "vendorSku": "SPR-GADGET-002", "quantity": 5 }
  ]
}
```

### Example 2: Drop Ship Order (Full)

```json
{
  "sourcePlatform": "Merchant360",
  "organizationId": 1,
  "merchantId": 100,
  "partnerConnectionId": 50,
  "externalOrderId": "M360-ORDER-12346",
  "correlationId": "550e8400-e29b-41d4-a716-446655440001",
  "idempotencyKey": "M360-2024-06-06-ORDER-12346",
  "submittedBy": "merchant-user@example.com",

  "poNumber": "PO-2024-002",
  "orderDate": "2024-06-06T10:00:00Z",
  "notes": "Leave at front door if no answer",

  "shipTo": {
    "name": "John Smith",
    "address1": "456 Oak Avenue",
    "address2": "Apt 2B",
    "city": "Naperville",
    "state": "IL",
    "postalCode": "60540",
    "phone": "630-555-1234",
    "email": "john@example.com",
    "isResidential": true
  },

  "lines": [
    {
      "lineNumber": 1,
      "vendorSku": "SPR-CHAIR-BLK",
      "buyerSku": "MY-CHAIR-001",
      "quantity": 1,
      "unitPrice": 299.99,
      "description": "Executive Office Chair - Black"
    }
  ],

  "orderType": "DropShip",
  "allowPartialShipment": false,
  "allowBackorder": false,
  "shippingPriority": "Standard",
  "shippingMethod": "Ground",
  "requestedDeliveryDate": "2024-06-12"
}
```

### Example 3: Wrap and Label Order

```json
{
  "sourcePlatform": "Merchant360",
  "organizationId": 1,
  "merchantId": 100,
  "partnerConnectionId": 50,
  "externalOrderId": "M360-ORDER-12347",
  "correlationId": "550e8400-e29b-41d4-a716-446655440002",
  "idempotencyKey": "M360-2024-06-06-ORDER-12347",

  "poNumber": "PO-2024-003",
  "notes": "Include dealer branding materials. Packing slip should show 'ABC Hardware' as seller.",

  "shipTo": {
    "name": "Jane Doe",
    "company": "Doe Construction",
    "address1": "789 Industrial Blvd",
    "city": "Aurora",
    "state": "IL",
    "postalCode": "60505",
    "phone": "630-555-5678",
    "email": "jane@doeconstruction.com",
    "isResidential": false
  },

  "billTo": {
    "company": "ABC Hardware Store",
    "address1": "123 Main Street",
    "city": "Chicago",
    "state": "IL",
    "postalCode": "60601"
  },

  "lines": [
    { "vendorSku": "SPR-DRILL-PRO", "quantity": 2, "unitPrice": 149.99 },
    { "vendorSku": "SPR-BIT-SET", "quantity": 1, "unitPrice": 49.99 }
  ],

  "orderType": "WrapAndLabel",
  "allowPartialShipment": false,
  "allowSubstitutions": false,
  "shippingPriority": "Expedited"
}
```

### Example 4: Success Response (202 Accepted)

```json
{
  "accepted": true,
  "partnerConnectOrderId": 12345,
  "partnerDocumentId": null,
  "status": "Accepted",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "acceptedAt": "2024-06-06T10:00:05Z",
  "warnings": [],
  "errors": [],
  "isDuplicate": false
}
```

### Example 5: Duplicate Response (200 OK)

```json
{
  "accepted": true,
  "partnerConnectOrderId": 12345,
  "partnerDocumentId": 67890,
  "status": "Duplicate",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "acceptedAt": "2024-06-06T09:55:00Z",
  "warnings": [],
  "errors": [],
  "isDuplicate": true
}
```

### Example 6: Validation Failure (400 Bad Request)

```json
{
  "accepted": false,
  "partnerConnectOrderId": null,
  "partnerDocumentId": null,
  "status": "ValidationFailed",
  "correlationId": "550e8400-e29b-41d4-a716-446655440003",
  "acceptedAt": null,
  "warnings": [],
  "errors": [
    {
      "code": "REQUIRED_FIELD",
      "field": "PoNumber",
      "message": "Purchase order number is required"
    },
    {
      "code": "REQUIRED_FIELD",
      "field": "Lines[0].VendorSku",
      "message": "Vendor SKU is required on line 1"
    }
  ],
  "isDuplicate": false
}
```

### Example 7: Idempotency Conflict (409 Conflict)

```json
{
  "accepted": false,
  "partnerConnectOrderId": 12345,
  "partnerDocumentId": null,
  "status": "Conflict",
  "correlationId": "550e8400-e29b-41d4-a716-446655440004",
  "acceptedAt": null,
  "warnings": [],
  "errors": [
    {
      "code": "IDEMPOTENCY_CONFLICT",
      "field": "IdempotencyKey",
      "message": "Order already exists with different content"
    }
  ],
  "isDuplicate": false
}
```

---

## M360 Implementation Guide

### What M360 Must Maintain

1. **ID Mappings**: M360 must store PC organizationId, merchantId, and partnerConnectionId values (provisioned during onboarding)
2. **External Order ID**: Generate unique `externalOrderId` for each order
3. **Idempotency Key**: Generate unique `idempotencyKey` for retry safety
4. **Correlation ID**: Pass `correlationId` from request context for tracing

### What M360 Must Persist from Response

1. **partnerConnectOrderId**: Required for all subsequent queries
2. **correlationId**: For log correlation
3. **status**: Current order status
4. **acceptedAt**: Timestamp for audit
5. **warnings**: Any warnings for user display
6. **isDuplicate**: Track if this was a retry

### What M360 Should Validate Before Submission

1. Product availability (if real-time inventory available)
2. Customer credit/payment status
3. Address completeness
4. Valid partner connection

### What M360 Should Never Send

- SPR-specific codes (EnterpriseCode, OrgCodes)
- EDI segment details
- Transport configuration
- ShipNode assignments
- DraftOrderFlag values

### Async Processing

- **Acceptance != Completion**: 202 Accepted means queued for processing
- **Poll or Webhook**: M360 should poll for status or receive webhooks
- **Status Transitions**: Submitted → Acknowledged → Processing → Shipped → Completed

---

## PartnerConnect Guarantees

### On Successful Acceptance (202 Accepted)

1. Order is persisted with unique `partnerConnectOrderId`
2. Order will be processed for EDI generation
3. Status updates will be available via API/webhook
4. Idempotency key is locked for this organization

### Validation Guarantees

1. All routing context validated before business rules
2. Organization active status checked
3. Tenant belongs to organization verified
4. Partner connection exists and is active
5. Business fields validated before persistence

### Partner Resolution Guarantees

1. Partner configuration resolved from TenantPartnerAccount
2. SPR-specific mappings applied internally
3. Business options translated to partner flags
4. No SPR details leaked back to M360

### Idempotency Guarantees

1. Same key + same content = returns existing order (200 OK)
2. Same key + different content = conflict error (409)
3. Idempotency scoped to organization
4. No duplicate orders created on retry

---

## OpenAPI Specification Summary

```yaml
openapi: 3.0.3
info:
  title: PartnerConnect Integration API
  version: 1.0.0
  description: Canonical order submission API for M360 integration

paths:
  /api/integrations/orders:
    post:
      summary: Submit a supplier order
      operationId: submitSupplierOrder
      tags:
        - Orders
      security:
        - BearerAuth: []
        - ApiKeyAuth: []
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/SubmitSupplierOrderRequest'
      responses:
        '202':
          description: Order accepted for processing
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/SubmitSupplierOrderResponse'
        '200':
          description: Duplicate submission (idempotent return)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/SubmitSupplierOrderResponse'
        '400':
          description: Validation failed
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/SubmitSupplierOrderResponse'
        '409':
          description: Idempotency conflict
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/SubmitSupplierOrderResponse'

components:
  securitySchemes:
    BearerAuth:
      type: http
      scheme: bearer
    ApiKeyAuth:
      type: apiKey
      in: header
      name: X-API-Key

  schemas:
    SubmitSupplierOrderRequest:
      type: object
      required:
        - sourcePlatform
        - organizationId
        - merchantId
        - partnerConnectionId
        - externalOrderId
        - correlationId
        - idempotencyKey
        - poNumber
        - shipTo
        - lines
      properties:
        sourcePlatform:
          type: string
          example: "Merchant360"
        organizationId:
          type: integer
          example: 1
        merchantId:
          type: integer
          example: 100
        partnerConnectionId:
          type: integer
          example: 50
        externalOrderId:
          type: string
          example: "M360-ORDER-12345"
        correlationId:
          type: string
          format: uuid
        idempotencyKey:
          type: string
          example: "M360-2024-06-06-ORDER-12345"
        submittedBy:
          type: string
        poNumber:
          type: string
          example: "PO-2024-001"
        orderDate:
          type: string
          format: date-time
        notes:
          type: string
        shipTo:
          $ref: '#/components/schemas/CanonicalAddressInfo'
        billTo:
          $ref: '#/components/schemas/CanonicalAddressInfo'
        lines:
          type: array
          items:
            $ref: '#/components/schemas/CanonicalOrderLineRequest'
          minItems: 1
        orderType:
          type: string
          enum: [StockOrder, DropShip, WrapAndLabel]
          default: StockOrder
        allowPartialShipment:
          type: boolean
          default: true
        allowBackorder:
          type: boolean
          default: true
        allowSubstitutions:
          type: boolean
          default: false
        shippingPriority:
          type: string
          enum: [Standard, Expedited, NextDay, Freight]
        requestedShipDate:
          type: string
          format: date
        requestedDeliveryDate:
          type: string
          format: date
        shippingMethod:
          type: string

    CanonicalAddressInfo:
      type: object
      required:
        - address1
        - city
        - state
        - postalCode
      properties:
        name:
          type: string
        company:
          type: string
        address1:
          type: string
        address2:
          type: string
        city:
          type: string
        state:
          type: string
          maxLength: 2
        postalCode:
          type: string
        country:
          type: string
          default: "US"
        phone:
          type: string
        email:
          type: string
          format: email
        isResidential:
          type: boolean

    CanonicalOrderLineRequest:
      type: object
      required:
        - vendorSku
        - quantity
      properties:
        lineNumber:
          type: integer
        vendorSku:
          type: string
        buyerSku:
          type: string
        upc:
          type: string
        quantity:
          type: number
          minimum: 0.01
        unitOfMeasure:
          type: string
          default: "EA"
        unitPrice:
          type: number
        description:
          type: string
        requestedDeliveryDate:
          type: string
          format: date
        notes:
          type: string
        externalLineReference:
          type: string

    SubmitSupplierOrderResponse:
      type: object
      properties:
        accepted:
          type: boolean
        partnerConnectOrderId:
          type: integer
        partnerDocumentId:
          type: integer
        status:
          type: string
        correlationId:
          type: string
        acceptedAt:
          type: string
          format: date-time
        warnings:
          type: array
          items:
            type: string
        errors:
          type: array
          items:
            $ref: '#/components/schemas/ValidationError'
        isDuplicate:
          type: boolean

    ValidationError:
      type: object
      properties:
        code:
          type: string
        field:
          type: string
        message:
          type: string
```

---

## Notes on Idempotency and Correlation IDs

### Idempotency Key Guidelines

- **Format**: `{sourcePlatform}-{date}-{orderId}` or similar unique pattern
- **Scope**: Unique per organization
- **Persistence**: Store on M360 side for retry scenarios
- **Conflict Resolution**: On 409 Conflict, investigate or generate new key

### Correlation ID Guidelines

- **Format**: UUID v4 recommended
- **Propagation**: Pass through all downstream services
- **Logging**: Include in all log entries for tracing
- **Response Echo**: PC echoes back for confirmation

---

## Change Log

| Version | Date | Changes |
|---------|------|---------|
| 1.0.0 | 2026-06-06 | Initial frozen contract for M360 consumption |

---

*This document is the authoritative source for M360 → PartnerConnect integration.*
