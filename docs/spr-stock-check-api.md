# PartnerConnect API — SPR Live Stock / Price Check (for Merchant360)

This is the M360-facing API for a **live, real-time SPR stock and price check**. M360 calls
PartnerConnect (PC); PC translates to SPR's web services, applies the dealer's account-specific
pricing, and returns a clean JSON result. M360 never talks SOAP or holds SPR credentials.

> Status: stock/price check is implemented. **Freight-rate endpoints are a planned Phase 2** and
> are not in this document yet.

---

## Authentication

All calls authenticate as the **organization** using the org's API key in a header:

```
X-Api-Key: <organization API key>
```

The same key the org is issued for PC↔org integration. A missing/invalid/inactive-org key returns
`401 Unauthorized`. (M360 is registered in PC as an organization.)

The **dealer** within the org is identified per-request by `externalTenantId` (the dealer's
org-side id — the same id M360 sends on that dealer's orders).

---

## Endpoint

### `POST /api/v1/org/stock-check`

Returns item attributes and per-distribution-center availability for an SPR item, including the
dealer's net price.

**Behavior**
- If `dcNumbers` is provided (1–8 DCs) → a lightweight check of just those DCs.
- If `dcNumbers` is omitted/empty → availability for **all** stocking DCs.
- **Dealer pricing is included automatically** because the dealer is resolved to their SPR account.
- The dealer **must have an active SPR connection** in PC (i.e., be subscribed to SPR). If not, the
  call returns `403`.

#### Request body — `StockCheckRequest`

| Field | Type | Required | Description |
|---|---|---|---|
| `externalTenantId` | string | yes | The dealer/tenant's org-side id. |
| `itemNumber` | string | yes | SPR item number = Manufacturer Id + Stock Number (e.g. `SPRW1011`). |
| `dcNumbers` | int[] | no | Up to 8 SPR DC numbers to check (e.g. `[1, 16, 2]`). Omit for all DCs. |
| `availableOnly` | bool | no | Default `true` — only return DCs with quantity available to sell. `false` returns all DCs. |

```json
{
  "externalTenantId": "3",
  "itemNumber": "SPRW1011",
  "dcNumbers": [1, 16],
  "availableOnly": true
}
```

#### Response body — `StockCheckResponse` (HTTP 200)

| Field | Type | Description |
|---|---|---|
| `success` | bool | `true` if SPR returned a successful result. |
| `message` | string | Partner status/error message (e.g. `"OK"`). |
| `itemNumber` | string | SPR item number. |
| `upc` | string | UPC. |
| `description` | string | Item short description. |
| `itemStatus` | string | SPR item status code (e.g. `A` = active). |
| `unitOfMeasure` | string | Selling unit of measure (e.g. `EA`). |
| `orderMinimum` | int | Minimum order quantity. |
| `retailPrice` | decimal | Manufacturer suggested retail price. |
| `hazmatMessage` | string | Hazmat handling note, if any. |
| `pricingIncluded` | bool | `true` when dealer pricing was returned. |
| `dealerPrice` | decimal | Dealer net unit price. |
| `discountable` | bool | Whether the dealer net price is discountable. |
| `priceDescription` | string | Pricing source/description. |
| `distributionCenters` | array | Per-DC availability (below). |

**`distributionCenters[]` — `DcAvailability`**

| Field | Type | Description |
|---|---|---|
| `dcNumber` | string | SPR DC number (e.g. `01`). |
| `dcName` | string | DC name (e.g. `ATLANTA`). |
| `available` | int | Quantity available to sell. |
| `unitOfMeasure` | string | DC selling unit of measure. |
| `onOrder` | int | Quantity on order with the manufacturer. |
| `expected` | string | Expected manufacturer delivery (days, or SPR codes like `DUE`/`LATE`). |
| `sprinter` | bool | DC serves as a Sprinter location. |
| `cutOff` | string | DC cutoff time to meet delivery. |
| `leadTime` | string | Delivery lead time to the Sprinter hub. |
| `dcType` | string | `ALL` (full line), `FURN` (furniture), `RDC` (re-distribution). |

```json
{
  "success": true,
  "message": "OK",
  "itemNumber": "SPRW1011",
  "upc": "035255004503",
  "description": "PAD,LEGAL,LTR SZ,WE",
  "itemStatus": "A",
  "unitOfMeasure": "EA",
  "orderMinimum": 1,
  "retailPrice": 3.32,
  "hazmatMessage": "No",
  "pricingIncluded": true,
  "dealerPrice": 0.99,
  "discountable": true,
  "priceDescription": "SMART CHOICE CATALOG",
  "distributionCenters": [
    { "dcNumber": "01", "dcName": "ATLANTA", "available": 101, "unitOfMeasure": "EA",
      "onOrder": 288, "expected": "DUE", "sprinter": true, "cutOff": "05:00 pm",
      "leadTime": "1", "dcType": "ALL" },
    { "dcNumber": "16", "dcName": "BIRMINGHAM", "available": 627, "unitOfMeasure": "EA",
      "onOrder": 0, "expected": null, "sprinter": true, "cutOff": "05:30 pm",
      "leadTime": "1", "dcType": "ALL" }
  ]
}
```

#### Status codes

| Code | Meaning |
|---|---|
| `200 OK` | Call reached SPR. Inspect `success` for the partner result. |
| `400 Bad Request` | Missing `itemNumber` or `externalTenantId`. |
| `401 Unauthorized` | Missing/invalid `X-Api-Key`, or the org is not active. |
| `403 Forbidden` | The dealer has no active SPR connection (not subscribed). Body: `{ "error": "Tenant has no active SPR connection" }`. |
| `503 Service Unavailable` | SPR web services are not configured on the partner. |

---

## Notes for the M360 team

- **Item number** must be SPR's Mfr Id + Stock Number. M360 sends it as-is; PC does not map M360
  SKUs to SPR item numbers.
- **DC numbers**: 1–8 for the lightweight per-DC check; omit for all DCs. Pass them as integers
  (PC zero-pads as SPR requires).
- **Pricing** is always dealer-specific (tied to the dealer's SPR account); there is no
  "anonymous price."
- A non-`success` `200` (e.g. unknown item) returns `success: false` with SPR's `message` — handle
  that distinctly from HTTP errors.

---

## Freight rates

Two endpoints, same `X-Api-Key` org auth and the same active-SPR-connection gating as stock check.
Both take the same request body; **all rates** returns every qualifying option, **lowest rate**
returns at most one.

### `POST /api/v1/org/freight/rates` &nbsp;·&nbsp; `POST /api/v1/org/freight/lowest-rate`

#### Request body — `FreightRateRequest`

| Field | Type | Required | Description |
|---|---|---|---|
| `externalTenantId` | string | yes | The dealer/tenant's org-side id. |
| `shipFromDc` | int | yes | Ship-from SPR DC number. |
| `destinationState` | string | yes | Destination state/province code (e.g. `GA`). |
| `destinationZip` | string | yes | Destination postal/ZIP code. |
| `totalWeight` | decimal | yes | Total shipment weight (lbs). |
| `serviceLevel` | string | (recommended) | Service-level code (`00`–`09`; e.g. `04`=Ground, `01`=Next Day Air). Required by SPR for lowest-rate. |
| `carrier` | string | no | Carrier code (`UPS`, `FDX`, `PCS`, …). Omit for all carriers. |
| `residential` | bool | no | Destination is residential. |

```json
{
  "externalTenantId": "3",
  "shipFromDc": 8,
  "destinationState": "GA",
  "destinationZip": "30341",
  "totalWeight": 1.0,
  "serviceLevel": "01",
  "residential": false
}
```

#### Response body — `FreightRateResponse`

| Field | Type | Description |
|---|---|---|
| `success` | bool | `true` if SPR returned a result. |
| `message` | string | Partner status/error message. |
| `rates` | array | Rate options (below). Lowest-rate returns 0–1; rates returns 0–N. |

**`rates[]` — `FreightRateOption`**

| Field | Type | Description |
|---|---|---|
| `shipFromDc` | string | DC the rate ships from. |
| `carrier` | string | Carrier code (e.g. `UPS`). |
| `carrierDescription` | string | Human-readable service (e.g. `UPS NEXT DAY AIR SAVER`). |
| `shipVia` | string | SPR ship-via code (e.g. `UP2S`, `FXGD`). |
| `rate` | decimal | Freight rate. |
| `deliveryDays` | int | Estimated delivery days. |
| `numberOfCartons` | int | Estimated cartons. |
| `serviceLevel` | string | Service-level code. |
| `residential` | bool | Residential indicator. |

```json
{
  "success": true,
  "message": "OK",
  "rates": [
    { "shipFromDc": "08", "carrier": "UPS", "carrierDescription": "UPS NEXT DAY AIR SAVER",
      "shipVia": "UP2S", "rate": 31.66, "deliveryDays": 1, "numberOfCartons": 1,
      "serviceLevel": "5", "residential": false }
  ]
}
```

Status codes are the same as stock check (`200`/`400`/`401`/`403`/`503`).

> **Not available:** Sprinter Stock Check and Zip-Code (drop-ship) Stock Check are not yet released
> by SPR, so PC does not expose them.
