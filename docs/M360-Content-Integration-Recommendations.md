# Merchant360 Content Integration Recommendations

**Document Version:** 1.0
**Date:** 2026-05-20
**Author:** PartnerConnect Team

---

## Executive Summary

This document outlines recommendations for integrating PartnerConnect (PC) canonical content data with Merchant360 (M360). The goal is to enable efficient, complete transfer of enhanced product content while designing for future scalability to other dealer systems.

**Key Recommendation:** Transition from **Push** model to **Pull with Webhooks** model for better scalability and loose coupling.

---

## 1. Architecture Recommendation: Pull with Webhooks

### Current State (Push Model)
```
PC ──push──> M360
PC ──push──> Future Dealer 1  (requires PC code changes)
PC ──push──> Future Dealer 2  (requires PC code changes)
```

### Recommended State (Pull with Webhooks)
```
                    ┌─────────────────┐
                    │ PartnerConnect  │
                    │   Content API   │
                    └────────┬────────┘
                             │
           ┌─────────────────┼─────────────────┐
           │                 │                 │
           ▼                 ▼                 ▼
      ┌─────────┐      ┌─────────┐      ┌─────────┐
      │  M360   │      │ Dealer1 │      │ Dealer2 │
      │ (pulls) │      │ (pulls) │      │ (pulls) │
      └─────────┘      └─────────┘      └─────────┘
```

### Benefits
| Aspect | Push Model | Pull Model |
|--------|------------|------------|
| New consumer integration | PC code changes required | Use existing API |
| Consumer sync schedule | PC dictates | Consumer controls |
| Failure handling | PC must retry per consumer | Consumer retries |
| API versioning | Complex coordination | Standard versioning |
| Rate limiting | PC manages all outbound | Per-consumer inbound |

### Hybrid Approach Details

1. **PC exposes Content API** (read-only, paginated)
2. **PC sends webhook notifications** when content changes (notification only, no payload)
3. **Consumers pull data** at their convenience using the API

---

## 2. Data Comparison: PC Canonical vs M360 Schema

### 2.1 Main Product Content

| PC Field | M360 Field | Status | Notes |
|----------|------------|--------|-------|
| Sku / ProductId | StockNumber | ✅ Match | |
| LocaleId | Locale | ✅ Match | |
| Description1 | ProductName | ✅ Match | |
| Description2 | ShortDescription | ✅ Match | |
| MarketingText | LongDescription | ✅ Match | |
| BrandName | BrandName | ✅ Match | |
| ManufacturerName | ManufacturerName | ✅ Match | |
| ManufacturerPartNumber | ManufacturerPartNumber | ✅ Match | |
| Upc | UpcCode | ✅ Match | |
| (build from hierarchy) | CategoryPath | ✅ Match | |
| ImageUrl225 | ImageUrl225 | ✅ Match | |
| ImageUrl75 | ImageUrl75 | ✅ Match | |
| ImageUrl3 | ImageUrl3 | ✅ Match | |
| (from price feed) | Weight | ⚠️ Gap | Not in content |
| (from price feed) | Length | ⚠️ Gap | Not in content |
| (from price feed) | Width | ⚠️ Gap | Not in content |
| (from price feed) | Height | ⚠️ Gap | Not in content |
| ContentVersionDate | ContentVersion | ✅ Match | |
| **Keywords** | ❌ Missing | **ADD** | Search terms |
| **CountryOfOrigin** | ❌ Missing | **ADD** | Origin country |
| **UnspscCode** | ❌ Missing | **ADD** | Standard classification |
| **ProductType** | ❌ Missing | **ADD** | Product type |
| **ProductLine** | ❌ Missing | **ADD** | Product line |
| **ProductSeries** | ❌ Missing | **ADD** | Product series |
| **ManufacturerId** | ❌ Missing | Consider | Internal ID |
| **RecycledPercent** | ❌ Missing | Consider | Sustainability |

### 2.2 Specifications

| PC | M360 | Status |
|----|------|--------|
| Raw: spr.productattribute (5.3M rows) | PC_ProductSpecification | ✅ Compatible |
| Canonical: SpecificationsHtml | N/A | ⚠️ HTML not needed |

**Note:** PC will query raw attributes for structured Group/Name/Value data.

### 2.3 Features

| PC Field | M360 Field | Status |
|----------|------------|--------|
| FeatureGroup | Headline | ✅ Match |
| BulletText | Description | ✅ Match |
| SortOrder | DisplayOrder | ✅ Match |

### 2.4 Relationships

| PC Field | M360 Field | Status |
|----------|------------|--------|
| RelatedProductId | RelatedStockNumber | ✅ Match |
| RelationshipType | RelationshipType | ✅ Match |
| SortOrder | DisplayOrder | ✅ Match |
| **IsBidirectional** | ❌ Missing | Consider | Bidirectional flag |

### 2.5 Categories (New Requirement)

PC has full category hierarchy that M360 doesn't capture:

| PC Table | Description |
|----------|-------------|
| SprCategories | 621 categories with parent/child relationships |
| Fields: CategoryCode, CategoryName, ParentCategoryId, Level, FullPath |

**Recommendation:** M360 should add a categories table for filtering/navigation.

---

## 3. Recommendations for M360

### 3.1 Tables to ADD

#### PC_ProductCategory (New Table)
```sql
CREATE TABLE PC_ProductCategory (
    Id bigint PRIMARY KEY,
    TradingPartnerId int NOT NULL,
    CategoryCode nvarchar(50) NOT NULL,
    CategoryName nvarchar(255) NOT NULL,
    ParentCategoryId bigint NULL,  -- Self-referential FK
    Level int NOT NULL DEFAULT 0,
    FullPath nvarchar(500),
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL,
    UpdatedAt datetime2 NOT NULL,

    CONSTRAINT FK_Category_Parent FOREIGN KEY (ParentCategoryId)
        REFERENCES PC_ProductCategory(Id),
    CONSTRAINT UQ_Category_Code UNIQUE (TradingPartnerId, CategoryCode)
);
```

**Purpose:** Enable category-based filtering, navigation, and hierarchy display.

### 3.2 Tables to DELETE

None. All existing tables are needed.

### 3.3 Table UPDATES

#### PC_ProductContent - Add Columns
```sql
ALTER TABLE PC_ProductContent ADD
    Keywords nvarchar(max) NULL,           -- Search keywords
    CountryOfOrigin nvarchar(100) NULL,    -- Country of origin
    UnspscCode nvarchar(20) NULL,          -- UNSPSC classification
    ProductType nvarchar(100) NULL,        -- Product type
    ProductLine nvarchar(100) NULL,        -- Product line
    ProductSeries nvarchar(100) NULL,      -- Product series
    RecycledPercent decimal(5,2) NULL,     -- Recycled content %
    CategoryId bigint NULL;                -- FK to PC_ProductCategory

ALTER TABLE PC_ProductContent ADD
    CONSTRAINT FK_ProductContent_Category
    FOREIGN KEY (CategoryId) REFERENCES PC_ProductCategory(Id);
```

#### PC_ProductRelationship - Add Column
```sql
ALTER TABLE PC_ProductRelationship ADD
    IsBidirectional bit NOT NULL DEFAULT 0;  -- Is relationship bidirectional
```

### 3.4 API Contract Updates

#### Current BatchContentRequest - Updates Needed

**Add to product object:**
```json
{
  "stockNumber": "ABC123",
  "productName": "...",
  // ... existing fields ...

  // NEW FIELDS:
  "keywords": "stapler heavy duty office",
  "countryOfOrigin": "China",
  "unspscCode": "44121615",
  "productType": "Stapler",
  "productLine": "Commercial",
  "productSeries": "Heavy Duty",
  "recycledPercent": 25.0,
  "categoryCode": "STAPLERS-HD"
}
```

**Add to relationship object:**
```json
{
  "stockNumber": "DEF456",
  "relationshipType": "Accessory",
  "isBidirectional": false
}
```

### 3.5 NEW APIs (for Pull Model)

If transitioning to Pull model, M360 doesn't need new APIs - **PC exposes the APIs**.

However, M360 may want to add:

#### Content Sync Status API
```
GET /api/v1/partner-connect/content/sync-status?tradingPartnerId=1

Response:
{
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "lastSyncAt": "2024-01-15T10:30:00Z",
  "productsCount": 85127,
  "specificationsCount": 4500000,
  "featuresCount": 326000,
  "relationshipsCount": 1076000,
  "categoriesCount": 621
}
```

### 3.6 APIs to REMOVE

None. Keep existing batch push API for backward compatibility during transition.

---

## 4. PC Content API Specification (New)

PC should expose these read-only APIs for consumers:

### 4.1 List Products (Paginated)
```
GET /api/v1/content/{tradingPartnerCode}/products
    ?page=1
    &pageSize=500
    &since=2024-01-01T00:00:00Z  (optional - delta sync)
    &locale=EN_US

Response:
{
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "contentVersion": "2024.05.20",
  "locale": "EN_US",
  "page": 1,
  "pageSize": 500,
  "totalCount": 85127,
  "totalPages": 171,
  "products": [
    {
      "stockNumber": "ABC123",
      "productName": "Premium Stapler",
      "shortDescription": "Heavy-duty stapler",
      "longDescription": "<p>Detailed HTML...</p>",
      "brandName": "Swingline",
      "manufacturerName": "ACCO Brands",
      "manufacturerPartNumber": "SWI-74740",
      "upcCode": "074711747400",
      "categoryPath": "Office Supplies > Desk Accessories > Staplers",
      "categoryCode": "STAPLERS-HD",
      "imageUrl225": "https://...",
      "imageUrl75": "https://...",
      "imageUrl3": "https://...",
      "keywords": "stapler heavy duty office swingline",
      "countryOfOrigin": "China",
      "unspscCode": "44121615",
      "productType": "Stapler",
      "productLine": "Commercial",
      "productSeries": "Heavy Duty",
      "recycledPercent": 25.0,
      "specifications": [
        { "group": "Physical", "name": "Material", "value": "Metal", "order": 1 },
        { "group": "Performance", "name": "Capacity", "value": "210 sheets", "order": 2 }
      ],
      "features": [
        { "headline": "Heavy Duty", "description": "Staples up to 210 sheets", "order": 1 },
        { "headline": "Ergonomic", "description": "Soft-grip handle", "order": 2 }
      ],
      "relatedProducts": [
        { "stockNumber": "DEF456", "type": "Accessory", "isBidirectional": false },
        { "stockNumber": "GHI789", "type": "CrossSell", "isBidirectional": true }
      ],
      "updatedAt": "2024-05-20T10:30:00Z"
    }
  ]
}
```

### 4.2 Get Single Product
```
GET /api/v1/content/{tradingPartnerCode}/products/{stockNumber}
    ?locale=EN_US

Response: Single product object (same structure as above)
```

### 4.3 List Categories
```
GET /api/v1/content/{tradingPartnerCode}/categories
    ?locale=EN_US

Response:
{
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "categories": [
    {
      "categoryCode": "OFFICE",
      "categoryName": "Office Supplies",
      "parentCategoryCode": null,
      "level": 0,
      "fullPath": "Office Supplies",
      "productCount": 45000
    },
    {
      "categoryCode": "STAPLERS",
      "categoryName": "Staplers",
      "parentCategoryCode": "OFFICE",
      "level": 1,
      "fullPath": "Office Supplies > Staplers",
      "productCount": 1200
    }
  ]
}
```

### 4.4 Content Change Webhook (Notification Only)
```
POST {subscriber-url}/webhooks/content-updated

{
  "event": "content.updated",
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "contentVersion": "2024.05.20",
  "changedAt": "2024-05-20T10:30:00Z",
  "summary": {
    "productsAdded": 50,
    "productsUpdated": 200,
    "productsRemoved": 5
  }
}
```

Consumer then calls the Content API to pull the actual data.

---

## 5. Migration Path

### Phase 1: Enhance Current Push (Immediate)
1. PC adds missing fields to push payload
2. M360 adds new columns to existing tables
3. Continue using batch push API

### Phase 2: Add Pull API (Short-term)
1. PC implements Content API (read-only)
2. M360 implements pull-based sync
3. Run both push and pull in parallel

### Phase 3: Deprecate Push (Long-term)
1. Verify pull model works correctly
2. Deprecate batch push API
3. New consumers use pull model only

---

## 6. Data Volume Estimates

| Data Type | PC Count | Per Product Avg | Notes |
|-----------|----------|-----------------|-------|
| Products | 85,127 | 1 | Main records |
| Specifications | 5,300,000 | ~62 | Name/value pairs |
| Features | 326,105 | ~4 | Bullet points |
| Relationships | 1,076,008 | ~12 | Cross-references |
| Categories | 621 | - | Hierarchy |

**Batch Size Recommendation:** 500 products per request (including nested data)

**Full Sync Time Estimate:** ~170 batches × 2 seconds = ~6 minutes

---

## 7. Summary of Changes

### For M360

| Type | Item | Priority |
|------|------|----------|
| **ADD Table** | PC_ProductCategory | High |
| **ADD Columns** | PC_ProductContent: Keywords, CountryOfOrigin, UnspscCode, ProductType, ProductLine, ProductSeries, RecycledPercent, CategoryId | High |
| **ADD Column** | PC_ProductRelationship: IsBidirectional | Medium |
| **UPDATE API** | BatchContentRequest: Add new fields | High |
| **ADD API** | Content sync status endpoint | Low |

### For PartnerConnect

| Type | Item | Priority |
|------|------|----------|
| **ADD API** | Content API (products, categories) | High |
| **ADD** | Webhook notifications | Medium |
| **UPDATE** | Populate ManufacturerPartNumber in transformation | High |
| **UPDATE** | Query raw attributes for structured specs | High |

---

## 8. Open Questions

1. **Weight/Length/Width/Height:** These are in price feed, not content. Should M360 get dimensions from price feed sync instead?

2. **Image URLs:** PC stores Etilize CDN URLs. Should PC proxy/cache images, or M360 use Etilize URLs directly?

3. **Locale Support:** PC has EN_US data. When other locales are needed, how should M360 request them?

4. **Delta Sync:** Should PC track change timestamps per product for efficient delta sync?

---

## Appendix A: PC Canonical Schema

```
SprProductContent (85K)
├── SprProductFeatures (326K)
├── SprProductRelationships (1M)
├── SprProductSpecifications (85K - HTML, not used for M360)
└── SprCategories (621)

Raw spr.productattribute (5.3M) - Used for structured specs
```

## Appendix B: Current vs Recommended Architecture

### Current (Push)
```
┌──────────────┐     POST /batch      ┌──────────────┐
│PartnerConnect│ ─────────────────>   │  Merchant360 │
│   (pushes)   │                      │  (receives)  │
└──────────────┘                      └──────────────┘
```

### Recommended (Pull with Webhook)
```
┌──────────────┐                      ┌──────────────┐
│PartnerConnect│ <─── GET /products ──│  Merchant360 │
│ (serves API) │                      │   (pulls)    │
│              │ ─── webhook notify ─>│              │
└──────────────┘                      └──────────────┘
```
