namespace Vendorea.PartnerConnect.Contracts.DTOs.ProductContent;

/// <summary>
/// Represents product content data from a trading partner.
/// </summary>
public record ProductContentDto(
    string PartnerSku,
    string? Upc,
    string? ManufacturerPartNumber,
    string? BrandName,
    string? ProductName,
    string? ShortDescription,
    string? LongDescription,
    string? CategoryPath,
    IReadOnlyList<ProductImageDto>? Images,
    IReadOnlyList<ProductSpecificationDto>? Specifications,
    string? Weight,
    string? Dimensions,
    string? CountryOfOrigin);

public record ProductImageDto(
    string Url,
    string? ImageType,
    int? SortOrder,
    string? AltText);

public record ProductSpecificationDto(
    string Name,
    string Value,
    string? GroupName);
