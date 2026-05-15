# Merchant360 Integration Requirements

## Overview

This document describes what Merchant360 needs to build to fully integrate with PartnerConnect. PartnerConnect serves as a **platform-level** middleware/integration hub that:

1. Receives data from trading partners (e.g., SPR)
2. Parses and validates the data
3. Pushes processed data to Merchant360 for merchant consumption

---

## Architecture Summary

```
Trading Partners (SPR, etc.)
         │
         ▼
   PartnerConnect (Platform-Level Service)
   ┌─────────────────────────────────┐
   │ • Receives partner data         │
   │ • Parses price feeds            │
   │ • Parses enhanced content       │
   │ • Stores master data            │
   │ • Pushes to Merchant360         │
   │ • Operates ACROSS tenants       │
   └─────────────────────────────────┘
         │
         ▼ (Manual Push via Admin UI)
    Merchant360 (Multi-Tenant)
   ┌─────────────────────────────────┐
   │ • Receives data from PC         │
   │ • Stores in PC_* tables         │
   │ • Serves data to merchants      │
   │ • Each merchant = tenant        │
   └─────────────────────────────────┘
         │
         ▼
     Merchants
```

---

## Multi-Tenancy Model

**PartnerConnect is platform-level** - it operates outside/above the tenant boundary:

- PartnerConnect authenticates as a **platform service account**
- It can push data to ANY merchant across all tenants
- The `merchantId` in API calls determines which tenant receives the data
- Merchant360 validates that the target merchant exists and is active

```
┌─────────────────────────────────────────────────────┐
│                   PLATFORM LEVEL                     │
│  ┌─────────────────────────────────────────────┐    │
│  │           PartnerConnect                     │    │
│  │    (Service Account: partnerconnect-svc)    │    │
│  └─────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────┘
                         │
         ┌───────────────┼───────────────┐
         ▼               ▼               ▼
   ┌──────────┐   ┌──────────┐   ┌──────────┐
   │ Tenant A │   │ Tenant B │   │ Tenant C │
   │ Merchant │   │ Merchant │   │ Merchant │
   │   101    │   │   102    │   │   103    │
   └──────────┘   └──────────┘   └──────────┘
```

---

## Data Ownership Model

| Data Type | Ownership | Storage Model |
|-----------|-----------|---------------|
| **Merchants** | Merchant360 is source of truth | Merchant360 owns merchant records |
| **Trading Partners** | Shared reference data | `TradingPartners` table in Merchant360 |
| **Price Data** | PartnerConnect processes, Merchant360 stores copy | **Merchant-specific** (PC_MerchantPrices) |
| **Enhanced Content** | PartnerConnect processes, Merchant360 stores copy | **Shared/Global** (PC_ProductContent) |

---

## Part 1: API Endpoints Merchant360 Must Expose

### 1.1 Get Merchants (for PartnerConnect UI)

PartnerConnect Admin UI needs to display a dropdown of merchants when uploading price feeds.

**Endpoint:** `GET /api/v1/merchants`

**Authentication:** Bearer token (OAuth2 client credentials)

**Request:**
```http
GET /api/v1/merchants?activeOnly=true
Authorization: Bearer {access_token}
```

**Query Parameters:**
| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `activeOnly` | bool | No | false | Filter to active merchants only |

**Response:** `200 OK`
```json
[
  {
    "id": 101,
    "name": "ABC Equipment Co",
    "code": "ABC001",
    "isActive": true
  },
  {
    "id": 102,
    "name": "XYZ Supply Inc",
    "code": "XYZ002",
    "isActive": true
  }
]
```

**Response Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | int | Yes | Unique merchant identifier |
| `name` | string | Yes | Merchant display name |
| `code` | string | No | Merchant code/account number |
| `isActive` | bool | Yes | Whether merchant is active |

---

### 1.2 Get Trading Partners

Returns registered trading partners that can send data.

**Endpoint:** `GET /api/v1/trading-partners`

**Authentication:** Bearer token (OAuth2 client credentials)

**Request:**
```http
GET /api/v1/trading-partners
Authorization: Bearer {access_token}
```

**Response:** `200 OK`
```json
[
  {
    "id": 1,
    "code": "SPR",
    "name": "SPR (Strategic Partner Resources)",
    "isActive": true
  }
]
```

---

### 1.3 Push Price Data (Batch)

PartnerConnect pushes parsed price feed data for a specific merchant.

**Endpoint:** `POST /api/v1/merchants/{merchantId}/prices/batch`

**Authentication:** Bearer token (OAuth2 client credentials)

**Request:**
```http
POST /api/v1/merchants/101/prices/batch
Authorization: Bearer {access_token}
Content-Type: application/json
```

```json
{
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "sourceUploadId": 456,
  "uploadedAt": "2024-01-15T10:30:00Z",
  "items": [
    {
      "stockNumber": "ABC123",
      "productDescription": "Heavy Duty Widget",
      "netCost": 45.99,
      "retailListPrice": 89.99,
      "uom": "EA",
      "uomFactor": 1,
      "categoryCode": "WIDGETS",
      "subcategoryCode": "HEAVY-DUTY",
      "brandCode": "ACME",
      "manufacturerPartNumber": "ACME-W-001",
      "upcCode": "012345678901",
      "weight": 2.5,
      "length": 10.0,
      "width": 5.0,
      "height": 3.0,
      "isActive": true
    }
  ]
}
```

**Request Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `tradingPartnerId` | int | Yes | FK to TradingPartners table |
| `tradingPartnerCode` | string | Yes | Partner code (e.g., "SPR") for logging |
| `sourceUploadId` | int | Yes | PartnerConnect upload ID for traceability |
| `uploadedAt` | datetime | Yes | When the file was uploaded to PartnerConnect |
| `items` | array | Yes | Array of price records |

**Price Item Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `stockNumber` | string | Yes | Partner's stock/SKU number (primary key) |
| `productDescription` | string | Yes | Product description |
| `netCost` | decimal | Yes | Merchant's cost |
| `retailListPrice` | decimal | No | Suggested retail price |
| `uom` | string | No | Unit of measure (EA, CS, etc.) |
| `uomFactor` | int | No | Units per UOM |
| `categoryCode` | string | No | Category code |
| `subcategoryCode` | string | No | Subcategory code |
| `brandCode` | string | No | Brand code |
| `manufacturerPartNumber` | string | No | Manufacturer part number |
| `upcCode` | string | No | UPC barcode |
| `weight` | decimal | No | Weight in lbs |
| `length` | decimal | No | Length in inches |
| `width` | decimal | No | Width in inches |
| `height` | decimal | No | Height in inches |
| `isActive` | bool | No | Whether item is active (default: true) |

**Response:** `200 OK`
```json
{
  "success": true,
  "merchantId": 101,
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "recordsReceived": 1500,
  "recordsCreated": 200,
  "recordsUpdated": 1300,
  "recordsSkipped": 0,
  "errors": []
}
```

**Error Response:** `400 Bad Request`
```json
{
  "success": false,
  "error": "Invalid merchant ID",
  "details": "Merchant 101 not found or inactive"
}
```

**Error Response:** `401 Unauthorized`
```json
{
  "error": "invalid_token",
  "error_description": "The access token is expired or invalid"
}
```

---

### 1.4 Push Enhanced Content (Batch)

PartnerConnect pushes parsed enhanced content. This is **shared master data** - not merchant-specific.

**Endpoint:** `POST /api/v1/content/products/batch`

**Authentication:** Bearer token (OAuth2 client credentials)

**Request:**
```http
POST /api/v1/content/products/batch
Authorization: Bearer {access_token}
Content-Type: application/json
```

```json
{
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "contentVersion": "2024.01",
  "locale": "EN_US",
  "sourceUploadId": 789,
  "products": [
    {
      "stockNumber": "ABC123",
      "productName": "Heavy Duty Widget",
      "shortDescription": "Industrial-grade widget for heavy applications",
      "longDescription": "This heavy duty widget is designed for...",
      "brandName": "ACME",
      "manufacturerName": "ACME Industries",
      "manufacturerPartNumber": "ACME-W-001",
      "upcCode": "012345678901",
      "categoryPath": "Equipment > Widgets > Heavy Duty",
      "imageUrl225": "https://cdn.spr.com/images/ABC123_225.jpg",
      "imageUrl75": "https://cdn.spr.com/images/ABC123_75.jpg",
      "imageUrl3": "https://cdn.spr.com/images/ABC123_alt.jpg",
      "weight": 2.5,
      "length": 10.0,
      "width": 5.0,
      "height": 3.0,
      "specifications": [
        {
          "name": "Material",
          "value": "Stainless Steel",
          "group": "Physical"
        },
        {
          "name": "Voltage",
          "value": "120V",
          "group": "Electrical"
        }
      ],
      "features": [
        {
          "headline": "Durable Construction",
          "description": "Built to withstand heavy use"
        }
      ],
      "relatedProducts": [
        {
          "stockNumber": "ABC124",
          "relationshipType": "Accessory"
        },
        {
          "stockNumber": "ABC125",
          "relationshipType": "CrossSell"
        }
      ]
    }
  ]
}
```

**Request Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `tradingPartnerId` | int | Yes | FK to TradingPartners table |
| `tradingPartnerCode` | string | Yes | Partner code for logging |
| `contentVersion` | string | Yes | Content version identifier |
| `locale` | string | Yes | Locale (EN_US, EN_CA, ES_US, FR_CA) |
| `sourceUploadId` | int | Yes | PartnerConnect upload ID |
| `products` | array | Yes | Array of product content records |

**Product Content Fields:**
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `stockNumber` | string | Yes | Partner's stock number (primary key) |
| `productName` | string | Yes | Product name/title |
| `shortDescription` | string | No | Brief description |
| `longDescription` | string | No | Detailed description (may contain HTML) |
| `brandName` | string | No | Brand name |
| `manufacturerName` | string | No | Manufacturer name |
| `manufacturerPartNumber` | string | No | MPN |
| `upcCode` | string | No | UPC code |
| `categoryPath` | string | No | Full category path |
| `imageUrl225` | string | No | Primary image URL (large) |
| `imageUrl75` | string | No | Thumbnail image URL |
| `imageUrl3` | string | No | Additional image URL |
| `weight` | decimal | No | Weight in lbs |
| `length` | decimal | No | Length in inches |
| `width` | decimal | No | Width in inches |
| `height` | decimal | No | Height in inches |
| `specifications` | array | No | Product specifications |
| `features` | array | No | Product features/benefits |
| `relatedProducts` | array | No | Related product relationships |

**Response:** `200 OK`
```json
{
  "success": true,
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "contentVersion": "2024.01",
  "locale": "EN_US",
  "productsReceived": 5000,
  "productsCreated": 500,
  "productsUpdated": 4500,
  "specificationsProcessed": 25000,
  "featuresProcessed": 15000,
  "relationshipsProcessed": 10000,
  "errors": []
}
```

---

### 1.5 Push Inventory Updates (Batch) - Future

**Endpoint:** `POST /api/v1/merchants/{merchantId}/inventory/batch`

(Similar structure to prices - implement when needed)

---

## Part 2: Authentication - OAuth2 Client Credentials

PartnerConnect authenticates to Merchant360 using **OAuth2 Client Credentials** flow (machine-to-machine).

### 2.1 Token Endpoint

**Endpoint:** `POST /connect/token` (or your OAuth server endpoint)

**Request:**
```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
&client_id=partnerconnect-service
&client_secret={secret}
&scope=merchant360.prices.write merchant360.content.write merchant360.merchants.read
```

**Response:** `200 OK`
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIs...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

### 2.2 Service Account Setup

Create a service account in Merchant360 for PartnerConnect:

| Setting | Value |
|---------|-------|
| Client ID | `partnerconnect-service` |
| Client Secret | (generate secure secret) |
| Grant Type | Client Credentials |
| Scopes | `merchant360.prices.write`, `merchant360.content.write`, `merchant360.merchants.read`, `merchant360.trading-partners.read` |
| Access Level | Platform (cross-tenant) |

### 2.3 PartnerConnect Configuration

```json
{
  "Merchant360": {
    "BaseUrl": "http://localhost:5003",
    "TokenEndpoint": "http://localhost:5003/connect/token",
    "ClientId": "partnerconnect-service",
    "ClientSecret": "{secret}",
    "Scopes": "merchant360.prices.write merchant360.content.write merchant360.merchants.read"
  }
}
```

---

## Part 3: Database Schema for Merchant360

All PartnerConnect tables use the `PC_` prefix to clearly separate them from native Merchant360 data.

### 3.1 Trading Partners Reference Table

```sql
-- Trading partners that can send data via PartnerConnect
CREATE TABLE TradingPartners (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Code NVARCHAR(50) NOT NULL,
    Name NVARCHAR(200) NOT NULL,
    Description NVARCHAR(500),
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2,

    CONSTRAINT UQ_TradingPartners_Code UNIQUE (Code)
);

-- Seed initial data
INSERT INTO TradingPartners (Code, Name, Description) VALUES
('SPR', 'SPR (Strategic Partner Resources)', 'Wholesaler providing pricing and enhanced content');
```

### 3.2 Merchant-Specific Price Tables

Each merchant has their own prices from trading partners. Prices can vary by merchant based on their agreements.

```sql
-- Merchant-specific pricing from trading partners
-- NOTE: No TenantId needed - MerchantId implies the tenant
CREATE TABLE PC_MerchantPrices (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    MerchantId INT NOT NULL,
    TradingPartnerId INT NOT NULL,
    StockNumber NVARCHAR(100) NOT NULL,

    -- Product info (denormalized for query performance)
    ProductDescription NVARCHAR(500),
    BrandCode NVARCHAR(50),
    CategoryCode NVARCHAR(50),
    SubcategoryCode NVARCHAR(50),
    ManufacturerPartNumber NVARCHAR(100),
    UpcCode NVARCHAR(50),

    -- Pricing
    NetCost DECIMAL(18,4) NOT NULL,
    RetailListPrice DECIMAL(18,4),
    Uom NVARCHAR(20),
    UomFactor INT DEFAULT 1,

    -- Dimensions
    Weight DECIMAL(10,4),
    Length DECIMAL(10,4),
    Width DECIMAL(10,4),
    Height DECIMAL(10,4),

    -- Status & Metadata
    IsActive BIT NOT NULL DEFAULT 1,
    SourceUploadId INT,  -- Traceability to PartnerConnect upload
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    -- Foreign Keys
    CONSTRAINT FK_PC_MerchantPrices_TradingPartner
        FOREIGN KEY (TradingPartnerId) REFERENCES TradingPartners(Id),

    -- Unique constraint: one price per merchant/partner/stock
    CONSTRAINT UQ_PC_MerchantPrices_MerchantPartnerStock
        UNIQUE (MerchantId, TradingPartnerId, StockNumber)
);

-- Indexes for common queries
CREATE INDEX IX_PC_MerchantPrices_MerchantPartner
    ON PC_MerchantPrices (MerchantId, TradingPartnerId);

CREATE INDEX IX_PC_MerchantPrices_StockNumber
    ON PC_MerchantPrices (StockNumber);

CREATE INDEX IX_PC_MerchantPrices_Category
    ON PC_MerchantPrices (MerchantId, CategoryCode);
```

### 3.3 Shared Enhanced Content Tables

Enhanced content is **shared master data** - all merchants see the same product content for a given stock number.

```sql
-- Shared product content (NOT merchant-specific)
CREATE TABLE PC_ProductContent (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    TradingPartnerId INT NOT NULL,
    StockNumber NVARCHAR(100) NOT NULL,
    Locale NVARCHAR(10) NOT NULL DEFAULT 'EN_US',

    -- Basic info
    ProductName NVARCHAR(500),
    ShortDescription NVARCHAR(2000),
    LongDescription NVARCHAR(MAX),  -- May contain HTML

    -- Branding
    BrandName NVARCHAR(200),
    ManufacturerName NVARCHAR(200),
    ManufacturerPartNumber NVARCHAR(100),
    UpcCode NVARCHAR(50),

    -- Categorization
    CategoryPath NVARCHAR(500),

    -- Images (URLs to partner CDN - not stored locally)
    ImageUrl225 NVARCHAR(500),
    ImageUrl75 NVARCHAR(500),
    ImageUrl3 NVARCHAR(500),

    -- Dimensions
    Weight DECIMAL(10,4),
    Length DECIMAL(10,4),
    Width DECIMAL(10,4),
    Height DECIMAL(10,4),

    -- Metadata
    ContentVersion NVARCHAR(50),
    SourceUploadId INT,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

    -- Foreign Keys
    CONSTRAINT FK_PC_ProductContent_TradingPartner
        FOREIGN KEY (TradingPartnerId) REFERENCES TradingPartners(Id),

    -- Unique: one content record per partner/stock/locale
    CONSTRAINT UQ_PC_ProductContent_PartnerStockLocale
        UNIQUE (TradingPartnerId, StockNumber, Locale)
);

CREATE INDEX IX_PC_ProductContent_StockNumber
    ON PC_ProductContent (StockNumber);

CREATE INDEX IX_PC_ProductContent_TradingPartner
    ON PC_ProductContent (TradingPartnerId, Locale);
```

### 3.4 Product Specifications

```sql
CREATE TABLE PC_ProductSpecifications (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductContentId BIGINT NOT NULL,
    SpecificationGroup NVARCHAR(100),
    SpecificationName NVARCHAR(200) NOT NULL,
    SpecificationValue NVARCHAR(500),
    DisplayOrder INT DEFAULT 0,

    CONSTRAINT FK_PC_ProductSpecs_Content
        FOREIGN KEY (ProductContentId)
        REFERENCES PC_ProductContent(Id) ON DELETE CASCADE
);

CREATE INDEX IX_PC_ProductSpecs_ContentId
    ON PC_ProductSpecifications (ProductContentId);
```

### 3.5 Product Features

```sql
CREATE TABLE PC_ProductFeatures (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductContentId BIGINT NOT NULL,
    Headline NVARCHAR(200),
    Description NVARCHAR(2000),
    DisplayOrder INT DEFAULT 0,

    CONSTRAINT FK_PC_ProductFeatures_Content
        FOREIGN KEY (ProductContentId)
        REFERENCES PC_ProductContent(Id) ON DELETE CASCADE
);

CREATE INDEX IX_PC_ProductFeatures_ContentId
    ON PC_ProductFeatures (ProductContentId);
```

### 3.6 Product Relationships

```sql
CREATE TABLE PC_ProductRelationships (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductContentId BIGINT NOT NULL,
    RelatedStockNumber NVARCHAR(100) NOT NULL,
    RelationshipType NVARCHAR(50) NOT NULL,  -- Accessory, CrossSell, UpSell, Replacement
    DisplayOrder INT DEFAULT 0,

    CONSTRAINT FK_PC_ProductRelations_Content
        FOREIGN KEY (ProductContentId)
        REFERENCES PC_ProductContent(Id) ON DELETE CASCADE
);

CREATE INDEX IX_PC_ProductRelationships_ContentId
    ON PC_ProductRelationships (ProductContentId);

CREATE INDEX IX_PC_ProductRelationships_RelatedStock
    ON PC_ProductRelationships (RelatedStockNumber);
```

### 3.7 Sync Audit Log

Track all data received from PartnerConnect.

```sql
CREATE TABLE PC_SyncLog (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    SyncType NVARCHAR(50) NOT NULL,  -- 'Prices', 'Content', 'Inventory'
    TradingPartnerId INT NOT NULL,
    MerchantId INT NULL,  -- NULL for shared content
    SourceUploadId INT NOT NULL,  -- PartnerConnect upload ID
    RecordsReceived INT NOT NULL,
    RecordsCreated INT NOT NULL,
    RecordsUpdated INT NOT NULL,
    RecordsFailed INT NOT NULL,
    StartedAt DATETIME2 NOT NULL,
    CompletedAt DATETIME2,
    Status NVARCHAR(50) NOT NULL,  -- 'InProgress', 'Completed', 'Failed'
    ErrorMessage NVARCHAR(MAX),
    RequestPayloadSize INT,  -- Bytes

    CONSTRAINT FK_PC_SyncLog_TradingPartner
        FOREIGN KEY (TradingPartnerId) REFERENCES TradingPartners(Id)
);

CREATE INDEX IX_PC_SyncLog_TradingPartner
    ON PC_SyncLog (TradingPartnerId, SyncType);

CREATE INDEX IX_PC_SyncLog_Merchant
    ON PC_SyncLog (MerchantId) WHERE MerchantId IS NOT NULL;
```

---

## Part 4: Data Flow Summary

### 4.1 Price Feed Flow

```
1. Admin uploads price CSV in PartnerConnect Admin Portal
2. Admin selects target Merchant from dropdown (fetched from Merchant360)
3. PartnerConnect parses and validates the file
4. PartnerConnect stores records in local tables
5. Admin reviews data, clicks "Push to Merchant360"
6. PartnerConnect authenticates via OAuth2 (gets access token)
7. PartnerConnect calls: POST /api/v1/merchants/{merchantId}/prices/batch
8. Merchant360 validates merchant exists and is active
9. Merchant360 upserts records into PC_MerchantPrices table
10. Merchant360 logs sync in PC_SyncLog
11. Merchant can now see updated prices
```

### 4.2 Enhanced Content Flow

```
1. Admin uploads content ZIP in PartnerConnect Admin Portal
2. PartnerConnect parses XML files and validates data
3. PartnerConnect stores records in local tables
4. Admin reviews data, clicks "Push to Merchant360"
5. PartnerConnect authenticates via OAuth2 (gets access token)
6. PartnerConnect calls: POST /api/v1/content/products/batch
7. Merchant360 upserts records into PC_ProductContent tables
8. Merchant360 logs sync in PC_SyncLog
9. ALL merchants can now see enhanced content (shared data)
```

---

## Part 5: Configuration

### 5.1 PartnerConnect Configuration

In PartnerConnect's `appsettings.Development.json`:

```json
{
  "Merchant360": {
    "BaseUrl": "http://localhost:5003",
    "TokenEndpoint": "http://localhost:5003/connect/token",
    "ClientId": "partnerconnect-service",
    "ClientSecret": "your-secret-here",
    "Scopes": "merchant360.prices.write merchant360.content.write merchant360.merchants.read",
    "TimeoutSeconds": 120
  }
}
```

For HTTPS in development:
```json
{
  "Merchant360": {
    "BaseUrl": "https://localhost:7089"
  }
}
```

### 5.2 Merchant360 Configuration

Register PartnerConnect as an OAuth2 client:

```json
{
  "OAuth2Clients": {
    "partnerconnect-service": {
      "ClientId": "partnerconnect-service",
      "ClientSecretHash": "{hashed-secret}",
      "AllowedGrantTypes": ["client_credentials"],
      "AllowedScopes": [
        "merchant360.merchants.read",
        "merchant360.trading-partners.read",
        "merchant360.prices.write",
        "merchant360.content.write"
      ],
      "AccessLevel": "Platform",
      "Description": "PartnerConnect integration service"
    }
  }
}
```

---

## Part 6: Error Handling

### 6.1 HTTP Status Codes

| Status | Meaning | When to Use |
|--------|---------|-------------|
| `200 OK` | Success | Request completed successfully |
| `400 Bad Request` | Invalid data | Validation errors, malformed JSON |
| `401 Unauthorized` | Auth failure | Invalid/expired token |
| `403 Forbidden` | Access denied | Valid token but insufficient scope |
| `404 Not Found` | Resource missing | Merchant not found |
| `422 Unprocessable Entity` | Business rule violation | e.g., Merchant is inactive |
| `500 Internal Server Error` | Server error | Unexpected errors |

### 6.2 Error Response Format

```json
{
  "success": false,
  "error": "MERCHANT_NOT_FOUND",
  "message": "Merchant with ID 999 was not found",
  "details": {
    "merchantId": 999
  },
  "traceId": "abc-123-def"
}
```

### 6.3 PartnerConnect Responsibilities

- Validate data before pushing
- Handle HTTP errors gracefully
- Retry transient failures (5xx) with exponential backoff
- Do NOT retry client errors (4xx)
- Log all push attempts with correlation IDs
- Show clear error messages in Admin UI

### 6.4 Merchant360 Responsibilities

- Validate incoming data thoroughly
- Return clear, actionable error messages
- Log all incoming requests with trace IDs
- Process batches transactionally (all or nothing)
- Return partial success details if supported

---

## Part 7: Future Considerations

### 7.1 Potential Future Endpoints

| Endpoint | Purpose |
|----------|---------|
| `DELETE /api/v1/merchants/{id}/prices` | Clear all prices for a merchant |
| `GET /api/v1/content/products/{stockNumber}` | Get single product content |
| `POST /api/v1/merchants/{id}/inventory/batch` | Push inventory levels |
| `POST /api/v1/orders` | Receive purchase orders from Merchant360 |

### 7.2 Potential Automations

- Scheduled sync (instead of manual push)
- Webhook notifications when data changes
- Delta/incremental syncs (only changed records)

### 7.3 Performance Considerations

For large batches (10,000+ records):
- Consider chunking into smaller batches
- Implement async processing with status polling
- Add compression for large payloads

---

## Summary Checklist for Merchant360

### Database
- [ ] Create `TradingPartners` table and seed SPR
- [ ] Create `PC_MerchantPrices` table (merchant-specific)
- [ ] Create `PC_ProductContent` table (shared)
- [ ] Create `PC_ProductSpecifications` table
- [ ] Create `PC_ProductFeatures` table
- [ ] Create `PC_ProductRelationships` table
- [ ] Create `PC_SyncLog` table

### Authentication
- [ ] Set up OAuth2 client credentials flow
- [ ] Create `partnerconnect-service` service account
- [ ] Configure platform-level access (cross-tenant)
- [ ] Provide client ID and secret to PartnerConnect team

### API Endpoints
- [ ] Implement `GET /api/v1/merchants`
- [ ] Implement `GET /api/v1/trading-partners`
- [ ] Implement `POST /api/v1/merchants/{id}/prices/batch`
- [ ] Implement `POST /api/v1/content/products/batch`

### Configuration
- [ ] Document base URL: `http://localhost:5003` (dev)
- [ ] Document token endpoint URL
- [ ] Provide client credentials to PartnerConnect team

---

## Part 8: Merchant Subscription Management

Merchants don't automatically get access to all trading partners' data. Access is granted on an approval basis through subscriptions.

### 8.1 Entity: MerchantTradingPartnerSubscription

```sql
CREATE TABLE MerchantTradingPartnerSubscriptions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    TenantId INT NOT NULL,                    -- Merchant (tenant)
    TradingPartnerId INT NOT NULL,            -- e.g., SPR
    AccountNumber NVARCHAR(100) NOT NULL,     -- Merchant's account with supplier
    Status NVARCHAR(20) NOT NULL,             -- Pending, Approved, Denied, Suspended

    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ApprovedAt DATETIME2 NULL,
    ApprovedByUserId INT NULL,
    DenialReason NVARCHAR(500) NULL,
    Notes NVARCHAR(1000) NULL,

    SuspendedAt DATETIME2 NULL,
    SuspendedByUserId INT NULL,

    CONSTRAINT FK_Subscription_TradingPartner
        FOREIGN KEY (TradingPartnerId) REFERENCES TradingPartners(Id),
    CONSTRAINT UQ_Subscription_TenantPartner
        UNIQUE (TenantId, TradingPartnerId)
);

CREATE INDEX IX_Subscription_Status ON MerchantTradingPartnerSubscriptions (Status);
CREATE INDEX IX_Subscription_TenantId ON MerchantTradingPartnerSubscriptions (TenantId);
```

### 8.2 API Endpoints for Subscription Management

#### List Subscriptions

**Endpoint:** `GET /api/v1/partner-connect/subscriptions`

**Query Parameters:**
| Parameter | Type | Description |
|-----------|------|-------------|
| `status` | string | Filter by status (Pending, Approved, Denied, Suspended) |
| `tenantId` | int | Filter by merchant |
| `tradingPartnerId` | int | Filter by trading partner |

**Response:** `200 OK`
```json
{
  "total": 25,
  "pendingCount": 3,
  "approvedCount": 18,
  "deniedCount": 2,
  "suspendedCount": 2,
  "items": [
    {
      "id": 1,
      "tenantId": 101,
      "tenantName": "ABC Equipment",
      "tenantCode": "MER001",
      "tradingPartnerId": 1,
      "tradingPartnerCode": "SPR",
      "tradingPartnerName": "Strategic Partner Resources",
      "accountNumber": "SPR-12345",
      "status": "Pending",
      "requestedAt": "2024-01-15T10:00:00Z",
      "approvedAt": null,
      "approvedByUserId": null,
      "approvedByUserName": null,
      "denialReason": null,
      "notes": null,
      "suspendedAt": null,
      "suspendedByUserId": null,
      "suspendedByUserName": null
    }
  ]
}
```

#### Get Single Subscription

**Endpoint:** `GET /api/v1/partner-connect/subscriptions/{id}`

**Response:** `200 OK` - Same structure as list item

#### Create Subscription (Admin-initiated)

**Endpoint:** `POST /api/v1/partner-connect/subscriptions`

**Request:**
```json
{
  "tenantId": 101,
  "tradingPartnerId": 1,
  "accountNumber": "SPR-12345",
  "notes": "Admin-initiated subscription"
}
```

**Response:** `201 Created` - Returns created subscription

#### Approve Subscription

**Endpoint:** `POST /api/v1/partner-connect/subscriptions/{id}/approve`

**Request:**
```json
{
  "notes": "Verified account number with SPR"
}
```

**Response:** `200 OK`
```json
{
  "success": true,
  "subscription": { /* updated subscription */ }
}
```

#### Deny Subscription

**Endpoint:** `POST /api/v1/partner-connect/subscriptions/{id}/deny`

**Request:**
```json
{
  "denialReason": "Invalid account number - not found in SPR system",
  "notes": "Contacted SPR to verify"
}
```

**Response:** `200 OK`

#### Suspend Subscription

**Endpoint:** `POST /api/v1/partner-connect/subscriptions/{id}/suspend`

**Request:**
```json
{
  "notes": "Account under review"
}
```

**Response:** `200 OK`

#### Reactivate Subscription

**Endpoint:** `POST /api/v1/partner-connect/subscriptions/{id}/reactivate`

**Response:** `200 OK` - Sets status back to Approved

### 8.3 OAuth2 Scope

Add new scope for subscription management:
- `merchant360.subscriptions.read` - View subscriptions
- `merchant360.subscriptions.write` - Manage subscriptions (approve/deny/suspend)

### 8.4 Subscription Checklist

- [ ] Create `MerchantTradingPartnerSubscriptions` table
- [ ] Implement `GET /api/v1/partner-connect/subscriptions` (list with filters)
- [ ] Implement `GET /api/v1/partner-connect/subscriptions/{id}` (single)
- [ ] Implement `POST /api/v1/partner-connect/subscriptions` (create)
- [ ] Implement `POST /api/v1/partner-connect/subscriptions/{id}/approve`
- [ ] Implement `POST /api/v1/partner-connect/subscriptions/{id}/deny`
- [ ] Implement `POST /api/v1/partner-connect/subscriptions/{id}/suspend`
- [ ] Implement `POST /api/v1/partner-connect/subscriptions/{id}/reactivate`
- [ ] Add subscription scopes to OAuth2 configuration

---

## Part 9: Trading Partner Sync Strategy

### 9.1 The Problem

There's a chicken-and-egg issue with subscriptions:

```
Merchant wants to subscribe
       │
       ▼
Needs to see available partners → Partner record must exist in M360
       │                                    │
       └──────── But "on data push" only ───┘
                 creates partner when PC
                 pushes data, which happens
                 AFTER subscription is approved
```

### 9.2 Solution: Two-Tier Sync Strategy

| Tier | Purpose | Data | Trigger |
|------|---------|------|---------|
| **Tier 1: Catalog Sync** | For subscription UI | Partner metadata only | M360 pulls on-demand |
| **Tier 2: Data Push Sync** | When pushing prices/content | Partner metadata + data | PC pushes with payload |

### 9.3 Tier 1: Partner Catalog Sync (Lightweight)

M360 pulls the list of available trading partners from PartnerConnect for the subscription UI.

**Endpoint:** `GET /api/v1/partners`

**Authentication:** OAuth2 Bearer token (client credentials)

**Request:**
```http
GET /api/v1/partners
Authorization: Bearer {access_token}
```

**Response:** `200 OK`
```json
[
  {
    "id": 1,
    "code": "SPR",
    "name": "S.P. Richards",
    "description": "Office products and supplies wholesaler",
    "logoUrl": "https://cdn.partnerconnect.com/logos/spr.png",
    "hasPriceData": true,
    "hasEnhancedContent": true,
    "isActive": true
  }
]
```

**Response Fields:**
| Field | Type | Description |
|-------|------|-------------|
| `id` | int | PartnerConnect ID (use as `PartnerConnectId` in M360) |
| `code` | string | Partner code (e.g., "SPR") |
| `name` | string | Display name |
| `description` | string | Partner description |
| `logoUrl` | string | URL to partner logo (nullable) |
| `hasPriceData` | bool | Whether PC has price data for this partner |
| `hasEnhancedContent` | bool | Whether PC has eContent for this partner |
| `isActive` | bool | Whether partner is active |

**When M360 should call this:**
- Scheduled job (hourly/daily) to keep local cache fresh
- Admin manual trigger via sync button
- Lazy load when merchant visits subscription UI (cache for X minutes)

**M360 Implementation:**
1. Create service method to call PC and upsert partners
2. Add endpoint `/api/v1/partner-connect/sync-partners` for admin trigger
3. Call before rendering subscription UI data (with caching)

### 9.4 Tier 2: Data Push Sync (Heavy)

When PartnerConnect pushes prices or content, include trading partner metadata in the payload. M360 auto-upserts the partner record.

**Updated Price Push Payload:**
```json
{
  "tradingPartner": {
    "partnerConnectId": 1,
    "code": "SPR",
    "name": "S.P. Richards",
    "description": "Office products and supplies wholesaler",
    "logoUrl": "https://cdn.partnerconnect.com/logos/spr.png"
  },
  "sourceUploadId": 456,
  "uploadedAt": "2024-01-15T10:30:00Z",
  "items": [...]
}
```

**Updated Content Push Payload:**
```json
{
  "tradingPartner": {
    "partnerConnectId": 1,
    "code": "SPR",
    "name": "S.P. Richards",
    "description": "Office products and supplies wholesaler",
    "logoUrl": "https://cdn.partnerconnect.com/logos/spr.png"
  },
  "contentVersion": "2024.01",
  "locale": "EN_US",
  "sourceUploadId": 789,
  "products": [...]
}
```

**M360 Processing:**
1. Extract `tradingPartner` from payload
2. Upsert into local `TradingPartners` table using `partnerConnectId` as key
3. Update `LastPriceSyncAt` or `LastContentSyncAt` timestamp
4. Process items as usual

### 9.5 Why M360 Pulls (Option A) vs PC Pushes (Option B)

| Aspect | M360 Pulls (Recommended) | PC Pushes |
|--------|--------------------------|-----------|
| Dependency direction | M360 → PC (already exists) | PC → M360 (new) |
| PC configuration | None needed | Needs M360 endpoint URL |
| Control | M360 controls refresh timing | PC controls timing |
| Complexity | Simpler | Requires webhook setup |
| Failure handling | M360 shows cached data | PC needs retry queues |

### 9.6 Enhanced TradingPartner Entity for M360

```sql
-- Updated TradingPartners table for M360
ALTER TABLE TradingPartners ADD
    PartnerConnectId INT NULL,              -- ID from PartnerConnect for mapping
    LogoUrl NVARCHAR(500) NULL,             -- Partner logo URL
    HasPriceData BIT NOT NULL DEFAULT 0,    -- Does PC have price data?
    HasEnhancedContent BIT NOT NULL DEFAULT 0, -- Does PC have eContent?
    LastPriceSyncAt DATETIME2 NULL,         -- When prices were last synced
    LastContentSyncAt DATETIME2 NULL,       -- When content was last synced
    LastSyncedFromPcAt DATETIME2 NULL;      -- When partner metadata was synced from PC

-- Index for PartnerConnect ID lookups
CREATE UNIQUE INDEX IX_TradingPartners_PartnerConnectId
    ON TradingPartners (PartnerConnectId)
    WHERE PartnerConnectId IS NOT NULL;
```

### 9.7 OAuth2 Scope

Add scope for M360 to read trading partners from PC:
- `partnerconnect.partners.read` - Read trading partner catalog

### 9.8 Trading Partner Sync Checklist

**PartnerConnect:**
- [ ] Enhance `GET /api/v1/partners` to include logoUrl, hasPriceData, hasEnhancedContent
- [ ] Add LogoUrl field to TradingPartner entity
- [ ] Update price push payload to include `tradingPartner` block
- [ ] Update content push payload to include `tradingPartner` block
- [ ] Support OAuth2 authentication on partners endpoint

**Merchant360:**
- [ ] Add missing fields to TradingPartner entity (PartnerConnectId, LogoUrl, etc.)
- [ ] Create service to call PC's `/api/v1/partners` endpoint
- [ ] Add `/api/v1/partner-connect/sync-partners` endpoint for admin trigger
- [ ] Update price receive handler to upsert trading partner from payload
- [ ] Update content receive handler to upsert trading partner from payload
- [ ] Cache partner list for subscription UI
