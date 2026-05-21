# Merchant360 Implementation Guide
## PartnerConnect Enhanced Content Integration

**Document Version:** 1.0
**Date:** 2026-05-20
**For:** Merchant360 Development Team

---

## Overview

This document provides implementation instructions for M360 to receive enhanced product content from PartnerConnect. Follow the sections in order.

**Estimated Effort:** 2-3 days

---

## 1. Database Changes

### 1.1 Create New Table: PC_ProductCategory

This table stores the category hierarchy for navigation and filtering.

```sql
-- Run this migration
CREATE TABLE PC_ProductCategory (
    Id bigint IDENTITY(1,1) PRIMARY KEY,
    TradingPartnerId int NOT NULL,
    CategoryCode nvarchar(50) NOT NULL,
    CategoryName nvarchar(255) NOT NULL,
    ParentCategoryId bigint NULL,
    Level int NOT NULL DEFAULT 0,
    FullPath nvarchar(500) NULL,
    ProductCount int NOT NULL DEFAULT 0,
    IsActive bit NOT NULL DEFAULT 1,
    CreatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt datetime2 NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT FK_ProductCategory_TradingPartner
        FOREIGN KEY (TradingPartnerId) REFERENCES TradingPartners(Id),
    CONSTRAINT FK_ProductCategory_Parent
        FOREIGN KEY (ParentCategoryId) REFERENCES PC_ProductCategory(Id),
    CONSTRAINT UQ_ProductCategory_Code
        UNIQUE (TradingPartnerId, CategoryCode)
);

-- Indexes
CREATE INDEX IX_ProductCategory_TradingPartner
    ON PC_ProductCategory(TradingPartnerId);
CREATE INDEX IX_ProductCategory_Parent
    ON PC_ProductCategory(ParentCategoryId);
```

### 1.2 Alter Table: PC_ProductContent

Add new columns for additional product data.

```sql
-- Add new columns
ALTER TABLE PC_ProductContent ADD
    Keywords nvarchar(max) NULL,
    CountryOfOrigin nvarchar(100) NULL,
    UnspscCode nvarchar(20) NULL,
    ProductType nvarchar(100) NULL,
    ProductLine nvarchar(100) NULL,
    ProductSeries nvarchar(100) NULL,
    RecycledPercent decimal(5,2) NULL,
    RecycledPcwPercent decimal(5,2) NULL,
    AssemblyRequired bit NULL,
    Description3 nvarchar(max) NULL,
    ManufacturerWebsite nvarchar(500) NULL,
    CategoryId bigint NULL;

-- Add foreign key to category
ALTER TABLE PC_ProductContent ADD
    CONSTRAINT FK_ProductContent_Category
    FOREIGN KEY (CategoryId) REFERENCES PC_ProductCategory(Id);

-- Index on category
CREATE INDEX IX_ProductContent_Category
    ON PC_ProductContent(CategoryId);

-- Index on keywords for full-text search (optional)
-- CREATE FULLTEXT INDEX ON PC_ProductContent(Keywords)
--     KEY INDEX PK_ProductContent;
```

### 1.3 Alter Table: PC_ProductRelationship

Add bidirectional flag.

```sql
ALTER TABLE PC_ProductRelationship ADD
    IsBidirectional bit NOT NULL DEFAULT 0;
```

---

## 2. API Contract Changes

### 2.1 Updated Request: BatchContentRequest

**Endpoint:** `POST /api/v1/partner-connect/content/products/batch`

#### Before (Current)
```json
{
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "contentVersion": "2024.01",
  "locale": "EN_US",
  "sourceUploadId": 12345,
  "products": [
    {
      "stockNumber": "ABC123",
      "productName": "Premium Stapler",
      "shortDescription": "Heavy-duty stapler",
      "longDescription": "<p>Detailed HTML description...</p>",
      "brandName": "Swingline",
      "manufacturerName": "ACCO Brands",
      "manufacturerPartNumber": "SWI-74740",
      "upcCode": "074711747400",
      "categoryPath": "Office Supplies > Desk Accessories > Staplers",
      "imageUrl225": "https://cdn.partner.com/images/ABC123_lg.jpg",
      "imageUrl75": "https://cdn.partner.com/images/ABC123_sm.jpg",
      "imageUrl3": "https://cdn.partner.com/images/ABC123_alt.jpg",
      "weight": 1.25,
      "length": 8.5,
      "width": 3.0,
      "height": 4.0,
      "specifications": [
        { "name": "Material", "value": "Metal", "group": "Physical" }
      ],
      "features": [
        { "headline": "Heavy Duty", "description": "Staples up to 210 sheets" }
      ],
      "relatedProducts": [
        { "stockNumber": "DEF456", "relationshipType": "Accessory" }
      ]
    }
  ]
}
```

#### After (Updated)
```json
{
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "contentVersion": "2024.01",
  "locale": "EN_US",
  "sourceUploadId": 12345,
  "products": [
    {
      "stockNumber": "ABC123",
      "productName": "Premium Stapler",
      "shortDescription": "Heavy-duty stapler",
      "longDescription": "<p>Detailed HTML description...</p>",
      "brandName": "Swingline",
      "manufacturerName": "ACCO Brands",
      "manufacturerPartNumber": "SWI-74740",
      "upcCode": "074711747400",
      "categoryPath": "Office Supplies > Desk Accessories > Staplers",
      "imageUrl225": "https://cdn.partner.com/images/ABC123_lg.jpg",
      "imageUrl75": "https://cdn.partner.com/images/ABC123_sm.jpg",
      "imageUrl3": "https://cdn.partner.com/images/ABC123_alt.jpg",
      "weight": 1.25,
      "length": 8.5,
      "width": 3.0,
      "height": 4.0,

      // NEW FIELDS - add these
      "keywords": "stapler heavy duty office swingline commercial",
      "countryOfOrigin": "China",
      "unspscCode": "44121615",
      "productType": "Stapler",
      "productLine": "Commercial",
      "productSeries": "Heavy Duty",
      "recycledPercent": 25.0,
      "recycledPcwPercent": 15.0,
      "assemblyRequired": false,
      "description3": "Additional specifications and details...",
      "manufacturerWebsite": "https://www.swingline.com",
      "categoryCode": "OFF-STAPLERS-HD",

      "specifications": [
        { "name": "Material", "value": "Metal", "group": "Physical", "displayOrder": 1 }
      ],
      "features": [
        { "headline": "Heavy Duty", "description": "Staples up to 210 sheets", "displayOrder": 1 }
      ],
      "relatedProducts": [
        {
          "stockNumber": "DEF456",
          "relationshipType": "Accessory",
          "isBidirectional": false  // NEW FIELD
        }
      ]
    }
  ]
}
```

### 2.2 New Fields Summary

#### Product Object - New Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `keywords` | string | No | Space-separated search keywords |
| `countryOfOrigin` | string | No | Country of origin (e.g., "China", "USA") |
| `unspscCode` | string | No | UNSPSC classification code |
| `productType` | string | No | Product type (e.g., "Stapler") |
| `productLine` | string | No | Product line (e.g., "Commercial") |
| `productSeries` | string | No | Product series (e.g., "Heavy Duty") |
| `recycledPercent` | decimal | No | Total recycled content percentage (0-100) |
| `recycledPcwPercent` | decimal | No | Post-consumer waste recycled percentage (0-100) |
| `assemblyRequired` | boolean | No | Whether product requires assembly |
| `description3` | string | No | Additional description/specifications text |
| `manufacturerWebsite` | string | No | Manufacturer website URL |
| `categoryCode` | string | No | Category code for FK to PC_ProductCategory |

#### RelatedProduct Object - New Fields
| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `isBidirectional` | boolean | No | If true, relationship exists both ways. Default: false |

### 2.3 Updated Response: BatchSyncResponse

No changes required. Current response format is sufficient.

---

## 3. New API: Category Batch Sync

### 3.1 Endpoint

```
POST /api/v1/partner-connect/content/categories/batch
```

**Authorization:** `CanWriteContent` policy

### 3.2 Request Contract

```json
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
      "isActive": true
    },
    {
      "categoryCode": "OFFICE-DESK",
      "categoryName": "Desk Accessories",
      "parentCategoryCode": "OFFICE",
      "level": 1,
      "fullPath": "Office Supplies > Desk Accessories",
      "isActive": true
    },
    {
      "categoryCode": "OFF-STAPLERS-HD",
      "categoryName": "Heavy Duty Staplers",
      "parentCategoryCode": "OFFICE-DESK",
      "level": 2,
      "fullPath": "Office Supplies > Desk Accessories > Heavy Duty Staplers",
      "isActive": true
    }
  ]
}
```

### 3.3 Response Contract

```json
{
  "success": true,
  "tradingPartnerId": 1,
  "tradingPartnerCode": "SPR",
  "categoriesReceived": 621,
  "categoriesCreated": 500,
  "categoriesUpdated": 121,
  "errors": []
}
```

### 3.4 Processing Logic

```csharp
// Pseudocode for category batch processing
public async Task<CategoryBatchResponse> ProcessCategoryBatch(CategoryBatchRequest request)
{
    var response = new CategoryBatchResponse { TradingPartnerId = request.TradingPartnerId };

    // First pass: Insert/update all categories without parent FK
    foreach (var cat in request.Categories)
    {
        var existing = await _db.Categories
            .FirstOrDefaultAsync(c => c.TradingPartnerId == request.TradingPartnerId
                                   && c.CategoryCode == cat.CategoryCode);

        if (existing == null)
        {
            _db.Categories.Add(new PC_ProductCategory
            {
                TradingPartnerId = request.TradingPartnerId,
                CategoryCode = cat.CategoryCode,
                CategoryName = cat.CategoryName,
                Level = cat.Level,
                FullPath = cat.FullPath,
                IsActive = cat.IsActive
            });
            response.CategoriesCreated++;
        }
        else
        {
            existing.CategoryName = cat.CategoryName;
            existing.Level = cat.Level;
            existing.FullPath = cat.FullPath;
            existing.IsActive = cat.IsActive;
            existing.UpdatedAt = DateTime.UtcNow;
            response.CategoriesUpdated++;
        }
    }
    await _db.SaveChangesAsync();

    // Second pass: Update parent references
    foreach (var cat in request.Categories.Where(c => c.ParentCategoryCode != null))
    {
        var category = await _db.Categories
            .FirstAsync(c => c.TradingPartnerId == request.TradingPartnerId
                          && c.CategoryCode == cat.CategoryCode);

        var parent = await _db.Categories
            .FirstOrDefaultAsync(c => c.TradingPartnerId == request.TradingPartnerId
                                   && c.CategoryCode == cat.ParentCategoryCode);

        category.ParentCategoryId = parent?.Id;
    }
    await _db.SaveChangesAsync();

    response.Success = true;
    response.CategoriesReceived = request.Categories.Count;
    return response;
}
```

---

## 4. Updated Processing Logic

### 4.1 Product Batch Processing Updates

Update your existing product batch processor to handle new fields:

```csharp
// In your existing ProcessProductBatch method, update the mapping:

private PC_ProductContent MapToEntity(ContentBatchProduct product, int tradingPartnerId)
{
    return new PC_ProductContent
    {
        // Existing fields
        TradingPartnerId = tradingPartnerId,
        StockNumber = product.StockNumber,
        Locale = product.Locale ?? "EN_US",
        ProductName = product.ProductName,
        ShortDescription = product.ShortDescription,
        LongDescription = product.LongDescription,
        BrandName = product.BrandName,
        ManufacturerName = product.ManufacturerName,
        ManufacturerPartNumber = product.ManufacturerPartNumber,
        UpcCode = product.UpcCode,
        CategoryPath = product.CategoryPath,
        ImageUrl225 = product.ImageUrl225,
        ImageUrl75 = product.ImageUrl75,
        ImageUrl3 = product.ImageUrl3,
        Weight = product.Weight,
        Length = product.Length,
        Width = product.Width,
        Height = product.Height,
        ContentVersion = product.ContentVersion,

        // NEW FIELDS - add these mappings
        Keywords = product.Keywords,
        CountryOfOrigin = product.CountryOfOrigin,
        UnspscCode = product.UnspscCode,
        ProductType = product.ProductType,
        ProductLine = product.ProductLine,
        ProductSeries = product.ProductSeries,
        RecycledPercent = product.RecycledPercent,
        RecycledPcwPercent = product.RecycledPcwPercent,
        AssemblyRequired = product.AssemblyRequired,
        Description3 = product.Description3,
        ManufacturerWebsite = product.ManufacturerWebsite,
        CategoryId = await GetCategoryId(tradingPartnerId, product.CategoryCode)
    };
}

private async Task<long?> GetCategoryId(int tradingPartnerId, string? categoryCode)
{
    if (string.IsNullOrEmpty(categoryCode)) return null;

    var category = await _db.Categories
        .FirstOrDefaultAsync(c => c.TradingPartnerId == tradingPartnerId
                               && c.CategoryCode == categoryCode);
    return category?.Id;
}
```

### 4.2 Relationship Processing Updates

```csharp
private PC_ProductRelationship MapRelationship(
    ContentRelatedProduct rel,
    long productContentId)
{
    return new PC_ProductRelationship
    {
        ProductContentId = productContentId,
        RelatedStockNumber = rel.StockNumber,
        RelationshipType = rel.RelationshipType,
        DisplayOrder = rel.DisplayOrder ?? 0,

        // NEW FIELD
        IsBidirectional = rel.IsBidirectional ?? false
    };
}
```

---

## 5. DTO Updates

### 5.1 ContentBatchProduct DTO

```csharp
public class ContentBatchProduct
{
    // Existing fields
    public string StockNumber { get; set; }
    public string? ProductName { get; set; }
    public string? ShortDescription { get; set; }
    public string? LongDescription { get; set; }
    public string? BrandName { get; set; }
    public string? ManufacturerName { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public string? UpcCode { get; set; }
    public string? CategoryPath { get; set; }
    public string? ImageUrl225 { get; set; }
    public string? ImageUrl75 { get; set; }
    public string? ImageUrl3 { get; set; }
    public decimal? Weight { get; set; }
    public decimal? Length { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public List<ContentSpecification> Specifications { get; set; }
    public List<ContentFeature> Features { get; set; }
    public List<ContentRelatedProduct> RelatedProducts { get; set; }

    // NEW FIELDS - add these
    public string? Keywords { get; set; }
    public string? CountryOfOrigin { get; set; }
    public string? UnspscCode { get; set; }
    public string? ProductType { get; set; }
    public string? ProductLine { get; set; }
    public string? ProductSeries { get; set; }
    public decimal? RecycledPercent { get; set; }
    public decimal? RecycledPcwPercent { get; set; }
    public bool? AssemblyRequired { get; set; }
    public string? Description3 { get; set; }
    public string? ManufacturerWebsite { get; set; }
    public string? CategoryCode { get; set; }
}
```

### 5.2 ContentRelatedProduct DTO

```csharp
public class ContentRelatedProduct
{
    public string StockNumber { get; set; }
    public string? RelationshipType { get; set; }
    public int? DisplayOrder { get; set; }

    // NEW FIELD - add this
    public bool? IsBidirectional { get; set; }
}
```

### 5.3 New DTOs for Categories

```csharp
public class CategoryBatchRequest
{
    public int TradingPartnerId { get; set; }
    public string TradingPartnerCode { get; set; }
    public List<CategoryBatchItem> Categories { get; set; } = new();
}

public class CategoryBatchItem
{
    public string CategoryCode { get; set; }
    public string CategoryName { get; set; }
    public string? ParentCategoryCode { get; set; }
    public int Level { get; set; }
    public string? FullPath { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CategoryBatchResponse
{
    public bool Success { get; set; }
    public int TradingPartnerId { get; set; }
    public string? TradingPartnerCode { get; set; }
    public int CategoriesReceived { get; set; }
    public int CategoriesCreated { get; set; }
    public int CategoriesUpdated { get; set; }
    public List<string> Errors { get; set; } = new();
}
```

---

## 6. Implementation Checklist

### Database
- [ ] Create `PC_ProductCategory` table
- [ ] Add columns to `PC_ProductContent`:
  - [ ] Keywords
  - [ ] CountryOfOrigin
  - [ ] UnspscCode
  - [ ] ProductType, ProductLine, ProductSeries
  - [ ] RecycledPercent, RecycledPcwPercent
  - [ ] AssemblyRequired
  - [ ] Description3
  - [ ] ManufacturerWebsite
  - [ ] CategoryId (FK)
- [ ] Add `IsBidirectional` to `PC_ProductRelationship`
- [ ] Add indexes
- [ ] Run migrations in dev/staging
- [ ] Run migrations in production

### DTOs
- [ ] Update `ContentBatchProduct` with new fields
- [ ] Update `ContentRelatedProduct` with `IsBidirectional`
- [ ] Create `CategoryBatchRequest` DTO
- [ ] Create `CategoryBatchItem` DTO
- [ ] Create `CategoryBatchResponse` DTO

### API
- [ ] Update product batch endpoint to accept new fields
- [ ] Create category batch endpoint
- [ ] Update Swagger documentation

### Processing Logic
- [ ] Update product mapping to handle new fields
- [ ] Update relationship mapping for `IsBidirectional`
- [ ] Implement category batch processing
- [ ] Implement `GetCategoryId` lookup

### Testing
- [ ] Test product batch with new fields (null values)
- [ ] Test product batch with new fields (populated values)
- [ ] Test category batch (new categories)
- [ ] Test category batch (update existing)
- [ ] Test category hierarchy (parent references)
- [ ] Test product-category FK relationship

---

## 7. Data Volumes

Expect the following data volumes from PartnerConnect:

| Data Type | Count | Notes |
|-----------|-------|-------|
| Products | ~85,000 | Per trading partner |
| Specifications | ~5,300,000 | ~62 per product average |
| Features | ~326,000 | ~4 per product average |
| Relationships | ~1,076,000 | ~12 per product average |
| Categories | ~621 | Hierarchical structure |

**Recommended batch size:** 500 products per request

---

## 8. Timeline

| Task | Estimate |
|------|----------|
| Database changes | 2 hours |
| DTO updates | 1 hour |
| API endpoint updates | 2 hours |
| Processing logic updates | 3 hours |
| Category API implementation | 3 hours |
| Testing | 4 hours |
| **Total** | **~2 days** |

---

## 9. Questions / Contact

For questions about this integration, contact the PartnerConnect team.

**Open items requiring M360 feedback:**
1. Should `Weight/Length/Width/Height` come from content sync or price feed sync?
2. Any additional fields needed that aren't listed here?
3. Preferred deployment timeline?
