# PartnerConnect Order Submission API
# M360 Integration Contract (FROZEN)

**Version:** 1.0.0
**Frozen:** 2026-06-06
**Status:** Ready for M360 Implementation

---

## 1. Endpoint

```
POST https://{environment}/api/integrations/orders
```

**Authentication:** OAuth2 Bearer token or API Key (`X-API-Key` header)
**Content-Type:** `application/json`

---

## 2. Request DTO

```json
{
  "sourcePlatform": "Merchant360",
  "organizationId": 1,
  "merchantId": 100,
  "partnerConnectionId": 50,
  "externalOrderId": "M360-ORDER-12345",
  "correlationId": "550e8400-e29b-41d4-a716-446655440000",
  "idempotencyKey": "M360-2024-06-06-ORDER-12345",
  "submittedBy": "user@merchant.com",

  "poNumber": "PO-2024-001",
  "orderDate": "2024-06-06T10:00:00Z",
  "notes": "Special instructions",
  "externalReferences": "{\"departmentCode\": \"ACCT\"}",

  "shipTo": {
    "name": "John Smith",
    "company": "Acme Corp",
    "address1": "123 Main Street",
    "address2": "Suite 100",
    "city": "Chicago",
    "state": "IL",
    "postalCode": "60601",
    "country": "US",
    "phone": "312-555-1234",
    "email": "john@acme.com",
    "isResidential": false
  },

  "billTo": null,

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

  "orderType": "StockOrder",
  "allowPartialShipment": true,
  "allowBackorder": true,
  "allowSubstitutions": false,
  "shippingPriority": "Standard",
  "requestedShipDate": "2024-06-07",
  "requestedDeliveryDate": "2024-06-10",
  "shippingMethod": "Ground"
}
```

### Field Reference

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `sourcePlatform` | string | **Yes** | Always `"Merchant360"` |
| `organizationId` | int | **Yes** | PartnerConnect organization ID |
| `merchantId` | int | **Yes** | PartnerConnect merchant/tenant ID |
| `partnerConnectionId` | int | **Yes** | Partner connection ID |
| `externalOrderId` | string | **Yes** | M360's order ID |
| `correlationId` | string | **Yes** | UUID for distributed tracing |
| `idempotencyKey` | string | **Yes** | Unique key for retry safety |
| `submittedBy` | string | No | Who submitted the order |
| `poNumber` | string | **Yes** | Customer's purchase order number |
| `orderDate` | datetime | No | Defaults to current UTC |
| `notes` | string | No | Order-level instructions |
| `externalReferences` | string | No | JSON object for additional IDs |
| `shipTo` | object | **Yes** | Ship-to address |
| `billTo` | object | No | Bill-to address (defaults to shipTo) |
| `lines` | array | **Yes** | At least one line required |
| `orderType` | string | No | `"StockOrder"` (default), `"DropShip"`, `"WrapAndLabel"` |
| `allowPartialShipment` | bool | No | Default: `true` |
| `allowBackorder` | bool | No | Default: `true` |
| `allowSubstitutions` | bool | No | Default: `false` |
| `shippingPriority` | string | No | `"Standard"`, `"Expedited"`, `"NextDay"`, `"Freight"` |
| `requestedShipDate` | date | No | Preferred ship date |
| `requestedDeliveryDate` | date | No | Preferred delivery date |
| `shippingMethod` | string | No | `"Ground"`, `"Express"`, etc. |

### Address Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | No | Contact name |
| `company` | string | No | Company name |
| `address1` | string | **Yes** | Street address |
| `address2` | string | No | Suite, unit, etc. |
| `city` | string | **Yes** | City |
| `state` | string | **Yes** | 2-letter state code |
| `postalCode` | string | **Yes** | ZIP/postal code |
| `country` | string | No | Default: `"US"` |
| `phone` | string | No | Contact phone |
| `email` | string | No | Contact email |
| `isResidential` | bool | No | Residential address flag |

### Line Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `lineNumber` | int | No | Auto-assigned if omitted |
| `vendorSku` | string | **Yes** | Partner's item ID |
| `buyerSku` | string | No | M360's item ID |
| `upc` | string | No | UPC/EAN barcode |
| `quantity` | decimal | **Yes** | Must be > 0 |
| `unitOfMeasure` | string | No | Default: `"EA"` |
| `unitPrice` | decimal | No | Unit price |
| `description` | string | No | Item description |
| `requestedDeliveryDate` | date | No | Line-level delivery date |
| `notes` | string | No | Line-level notes |
| `externalLineReference` | string | No | External line ID |

---

## 3. Response DTO

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

### Response Fields

| Field | Type | Description |
|-------|------|-------------|
| `accepted` | bool | `true` if order accepted |
| `partnerConnectOrderId` | int | PC order ID (null if rejected) |
| `partnerDocumentId` | int | EDI document ID (null initially) |
| `status` | string | `"Accepted"`, `"Duplicate"`, `"ValidationFailed"`, `"Conflict"` |
| `correlationId` | string | Echo of request correlationId |
| `acceptedAt` | datetime | When order was accepted |
| `warnings` | array | Non-blocking warnings |
| `errors` | array | Validation errors |
| `isDuplicate` | bool | `true` if idempotent return |

---

## 4. Sample Requests/Responses

### Stock Order (Minimal)

**Request:**
```json
{
  "sourcePlatform": "Merchant360",
  "organizationId": 1,
  "merchantId": 100,
  "partnerConnectionId": 50,
  "externalOrderId": "M360-ORD-001",
  "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "idempotencyKey": "M360-20240606-ORD-001",
  "poNumber": "PO-2024-001",
  "shipTo": {
    "company": "ABC Hardware",
    "address1": "123 Main St",
    "city": "Chicago",
    "state": "IL",
    "postalCode": "60601"
  },
  "lines": [
    { "vendorSku": "SPR-WIDGET-001", "quantity": 10 }
  ]
}
```

**Response (202 Accepted):**
```json
{
  "accepted": true,
  "partnerConnectOrderId": 12345,
  "partnerDocumentId": null,
  "status": "Accepted",
  "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "acceptedAt": "2024-06-06T10:00:05Z",
  "warnings": [],
  "errors": [],
  "isDuplicate": false
}
```

### Drop Ship Order

**Request:**
```json
{
  "sourcePlatform": "Merchant360",
  "organizationId": 1,
  "merchantId": 100,
  "partnerConnectionId": 50,
  "externalOrderId": "M360-ORD-002",
  "correlationId": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
  "idempotencyKey": "M360-20240606-ORD-002",
  "poNumber": "PO-2024-002",
  "shipTo": {
    "name": "Jane Doe",
    "address1": "456 Oak Ave",
    "city": "Naperville",
    "state": "IL",
    "postalCode": "60540",
    "phone": "630-555-1234",
    "isResidential": true
  },
  "lines": [
    { "vendorSku": "SPR-CHAIR-BLK", "quantity": 1, "unitPrice": 299.99 }
  ],
  "orderType": "DropShip",
  "allowPartialShipment": false,
  "shippingPriority": "Expedited"
}
```

**Response (202 Accepted):**
```json
{
  "accepted": true,
  "partnerConnectOrderId": 12346,
  "partnerDocumentId": null,
  "status": "Accepted",
  "correlationId": "b2c3d4e5-f6a7-8901-bcde-f23456789012",
  "acceptedAt": "2024-06-06T10:05:00Z",
  "warnings": [],
  "errors": [],
  "isDuplicate": false
}
```

### Duplicate Submission

**Response (200 OK):**
```json
{
  "accepted": true,
  "partnerConnectOrderId": 12345,
  "partnerDocumentId": 67890,
  "status": "Duplicate",
  "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "acceptedAt": "2024-06-06T10:00:05Z",
  "warnings": [],
  "errors": [],
  "isDuplicate": true
}
```

---

## 5. Validation Error Example

**Response (400 Bad Request):**
```json
{
  "accepted": false,
  "partnerConnectOrderId": null,
  "partnerDocumentId": null,
  "status": "ValidationFailed",
  "correlationId": "c3d4e5f6-a7b8-9012-cdef-345678901234",
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
    },
    {
      "code": "INVALID_VALUE",
      "field": "Lines[1].Quantity",
      "message": "Quantity must be greater than 0 on line 2"
    }
  ],
  "isDuplicate": false
}
```

### Idempotency Conflict

**Response (409 Conflict):**
```json
{
  "accepted": false,
  "partnerConnectOrderId": 12345,
  "partnerDocumentId": null,
  "status": "Conflict",
  "correlationId": "d4e5f6a7-b8c9-0123-def0-456789012345",
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

### Common Error Codes

| Code | Field | Description |
|------|-------|-------------|
| `REQUIRED_FIELD` | Various | Required field is missing |
| `INVALID_VALUE` | Various | Field value is invalid |
| `ORGANIZATION_NOT_FOUND` | OrganizationId | Org ID not found |
| `ORGANIZATION_NOT_ACTIVE` | OrganizationId | Org is suspended |
| `MERCHANT_NOT_FOUND` | MerchantId | Merchant not found |
| `MERCHANT_ORG_MISMATCH` | MerchantId | Merchant not in org |
| `MERCHANT_NOT_ACTIVE` | MerchantId | Merchant is suspended |
| `PARTNER_CONNECTION_NOT_FOUND` | PartnerConnectionId | Connection not found |
| `IDEMPOTENCY_CONFLICT` | IdempotencyKey | Same key, different content |

---

## 6. Fields M360 Must Persist

**On every response, persist these fields:**

| Field | Storage | Purpose |
|-------|---------|---------|
| `partnerConnectOrderId` | **Required** | Primary key for all subsequent operations |
| `correlationId` | **Required** | Log correlation across systems |
| `status` | **Required** | Current order status |
| `acceptedAt` | Recommended | Audit timestamp |
| `isDuplicate` | Recommended | Track retry scenarios |
| `warnings` | Recommended | Display to user or log |

**M360 should maintain a mapping table:**

```sql
CREATE TABLE PartnerConnectOrders (
    M360OrderId          INT PRIMARY KEY,
    PartnerConnectOrderId INT NOT NULL,
    CorrelationId        UNIQUEIDENTIFIER NOT NULL,
    IdempotencyKey       NVARCHAR(100) NOT NULL,
    Status               NVARCHAR(50) NOT NULL,
    AcceptedAt           DATETIME2,
    LastUpdatedAt        DATETIME2,
    IsDuplicate          BIT
);
```

---

## 7. Webhook/Callback Payloads

M360 should register a webhook endpoint to receive order status updates.

### Webhook Registration (Future)

```
POST /api/v1/webhooks
{
  "url": "https://merchant360.example.com/webhooks/partnerconnect",
  "events": ["order.acknowledged", "order.shipped", "order.completed", "order.failed"],
  "secret": "your-webhook-secret"
}
```

### Order Status Update Payload

```json
{
  "eventType": "order.acknowledged",
  "eventId": "evt_abc123",
  "timestamp": "2024-06-06T12:30:00Z",
  "data": {
    "partnerConnectOrderId": 12345,
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "externalOrderId": "M360-ORD-001",
    "previousStatus": "Submitted",
    "newStatus": "Acknowledged",
    "partnerOrderNumber": "SPR-PO-98765",
    "acknowledgedAt": "2024-06-06T12:30:00Z",
    "lineStatuses": [
      {
        "lineNumber": 1,
        "vendorSku": "SPR-WIDGET-001",
        "status": "Acknowledged",
        "quantityAcknowledged": 10,
        "expectedShipDate": "2024-06-07"
      }
    ]
  }
}
```

### Shipment Notification Payload

```json
{
  "eventType": "order.shipped",
  "eventId": "evt_def456",
  "timestamp": "2024-06-07T14:00:00Z",
  "data": {
    "partnerConnectOrderId": 12345,
    "correlationId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "externalOrderId": "M360-ORD-001",
    "previousStatus": "Acknowledged",
    "newStatus": "Shipped",
    "shipments": [
      {
        "shipmentId": "SHP-001",
        "carrier": "UPS",
        "trackingNumber": "1Z999AA10123456784",
        "shippedAt": "2024-06-07T14:00:00Z",
        "estimatedDelivery": "2024-06-10",
        "lines": [
          {
            "lineNumber": 1,
            "vendorSku": "SPR-WIDGET-001",
            "quantityShipped": 10
          }
        ]
      }
    ]
  }
}
```

### Order Failed Payload

```json
{
  "eventType": "order.failed",
  "eventId": "evt_ghi789",
  "timestamp": "2024-06-06T11:00:00Z",
  "data": {
    "partnerConnectOrderId": 12347,
    "correlationId": "e5f6a7b8-c9d0-1234-ef01-567890123456",
    "externalOrderId": "M360-ORD-003",
    "previousStatus": "Submitted",
    "newStatus": "Failed",
    "failureReason": "Partner rejected order: Invalid item SKU",
    "failedAt": "2024-06-06T11:00:00Z",
    "errors": [
      {
        "code": "PARTNER_REJECTION",
        "field": "Lines[0].VendorSku",
        "message": "Item SPR-INVALID-SKU not found in partner catalog"
      }
    ]
  }
}
```

### Status Flow

```
Submitted → Acknowledged → Processing → Shipped → Completed
    ↓           ↓             ↓           ↓
  Failed      Failed       Failed    PartiallyShipped → Shipped
                                           ↓
                                        Completed
```

### Polling Alternative

If webhooks are not configured, M360 can poll:

```
GET /api/integrations/orders/{partnerConnectOrderId}/status
```

**Response:**
```json
{
  "partnerConnectOrderId": 12345,
  "status": "Shipped",
  "partnerOrderNumber": "SPR-PO-98765",
  "lastUpdatedAt": "2024-06-07T14:00:00Z",
  "shipments": [...]
}
```

---

## Quick Reference

### Order Types (Fulfillment Mode)

| Value | Description |
|-------|-------------|
| `StockOrder` | Ship to dealer warehouse (default) |
| `DropShip` | Ship to end customer, no branding |
| `WrapAndLabel` | Ship to end customer with dealer branding |

### Shipping Priority (Service Urgency)

| Value | Description |
|-------|-------------|
| `Standard` | Normal delivery (default) |
| `Expedited` | Faster delivery |
| `NextDay` | Next business day |
| `Freight` | LTL/freight shipping |

### HTTP Response Codes

| Code | Meaning | Action |
|------|---------|--------|
| `202` | Accepted | Persist `partnerConnectOrderId` |
| `200` | Duplicate | Use existing `partnerConnectOrderId` |
| `400` | Validation failed | Fix errors and retry |
| `409` | Conflict | Investigate or generate new idempotencyKey |
| `401` | Auth failed | Check credentials |
| `500` | Server error | Retry with backoff |

---

**Contract frozen. Do not modify field names, DTO names, or endpoint routes without critical defect.**
