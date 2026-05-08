namespace Vendorea.PartnerConnect.Contracts.DTOs.CommercialData;

/// <summary>
/// Represents a single item in an inventory feed from a trading partner.
/// </summary>
public record InventoryItemDto(
    string PartnerSku,
    string? Upc,
    int QuantityAvailable,
    int? QuantityOnOrder,
    int? QuantityReserved,
    string? WarehouseCode,
    string? WarehouseName,
    string? AvailabilityStatus,
    DateTime? ExpectedRestockDate,
    DateTime? AsOfDate);
