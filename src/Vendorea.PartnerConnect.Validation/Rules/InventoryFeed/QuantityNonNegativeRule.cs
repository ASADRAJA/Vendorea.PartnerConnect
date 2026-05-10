using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Validation.Core;
using Vendorea.PartnerConnect.Validation.Interfaces;

namespace Vendorea.PartnerConnect.Validation.Rules.InventoryFeed;

/// <summary>
/// Validates that inventory quantities are non-negative.
/// </summary>
public class QuantityNonNegativeRule : ValidationRuleBase<InventoryUpdate>
{
    public override string RuleCode => "INVENTORY_QUANTITY_NON_NEGATIVE";
    public override string Description => "Validates that inventory quantities are non-negative";

    public override Task<ValidationResult> ValidateAsync(
        InventoryUpdate item,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        if (item.QuantityAvailable < 0)
        {
            result.AddError(RuleCode,
                $"QuantityAvailable cannot be negative (got {item.QuantityAvailable})",
                nameof(item.QuantityAvailable));
        }

        if (item.QuantityOnOrder.HasValue && item.QuantityOnOrder.Value < 0)
        {
            result.AddError(RuleCode,
                $"QuantityOnOrder cannot be negative (got {item.QuantityOnOrder.Value})",
                nameof(item.QuantityOnOrder));
        }

        if (item.QuantityReserved.HasValue && item.QuantityReserved.Value < 0)
        {
            result.AddError(RuleCode,
                $"QuantityReserved cannot be negative (got {item.QuantityReserved.Value})",
                nameof(item.QuantityReserved));
        }

        // Check if reserved exceeds available (warning)
        if (item.QuantityReserved.HasValue &&
            item.QuantityReserved.Value > item.QuantityAvailable)
        {
            result.AddWarning(RuleCode,
                $"QuantityReserved ({item.QuantityReserved.Value}) exceeds QuantityAvailable ({item.QuantityAvailable})",
                nameof(item.QuantityReserved));
        }

        return Task.FromResult(result);
    }
}

/// <summary>
/// Validates required fields for inventory updates.
/// </summary>
public class InventoryRequiredFieldsRule : ValidationRuleBase<InventoryUpdate>
{
    public override string RuleCode => "INVENTORY_REQUIRED_FIELDS";
    public override string Description => "Validates that all required fields are present";
    public override bool StopOnFailure => true;

    public override Task<ValidationResult> ValidateAsync(
        InventoryUpdate item,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(item.PartnerSku))
        {
            result.AddError(RuleCode, "PartnerSku is required", nameof(item.PartnerSku));
        }

        if (string.IsNullOrWhiteSpace(item.TradingPartnerCode))
        {
            result.AddError(RuleCode, "TradingPartnerCode is required", nameof(item.TradingPartnerCode));
        }

        if (item.DealerId <= 0)
        {
            result.AddError(RuleCode, "DealerId must be a positive number", nameof(item.DealerId));
        }

        // Either UPC or ManufacturerPartNumber should be present (warning if missing both)
        if (string.IsNullOrWhiteSpace(item.Upc) && string.IsNullOrWhiteSpace(item.ManufacturerPartNumber))
        {
            result.AddWarning(RuleCode, "Neither UPC nor ManufacturerPartNumber provided - matching may be less accurate");
        }

        return Task.FromResult(result);
    }
}

/// <summary>
/// Validates availability status consistency with quantities.
/// </summary>
public class AvailabilityStatusConsistencyRule : ValidationRuleBase<InventoryUpdate>
{
    public override string RuleCode => "INVENTORY_STATUS_CONSISTENCY";
    public override string Description => "Validates that availability status is consistent with quantities";

    public override Task<ValidationResult> ValidateAsync(
        InventoryUpdate item,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        switch (item.AvailabilityStatus)
        {
            case Canonical.Enums.AvailabilityStatus.InStock when item.QuantityAvailable <= 0:
                result.AddWarning(RuleCode,
                    $"Status is InStock but QuantityAvailable is {item.QuantityAvailable}",
                    nameof(item.AvailabilityStatus));
                break;

            case Canonical.Enums.AvailabilityStatus.OutOfStock when item.QuantityAvailable > 0:
                result.AddWarning(RuleCode,
                    $"Status is OutOfStock but QuantityAvailable is {item.QuantityAvailable}",
                    nameof(item.AvailabilityStatus));
                break;

            case Canonical.Enums.AvailabilityStatus.Backordered:
                if (!item.ExpectedRestockDate.HasValue)
                {
                    result.AddWarning(RuleCode,
                        "Status is Backordered but no ExpectedRestockDate provided",
                        nameof(item.ExpectedRestockDate));
                }
                break;
        }

        // Validate restock date is in the future
        if (item.ExpectedRestockDate.HasValue && item.ExpectedRestockDate.Value < DateTime.UtcNow.Date)
        {
            result.AddWarning(RuleCode,
                $"ExpectedRestockDate {item.ExpectedRestockDate.Value:yyyy-MM-dd} is in the past",
                nameof(item.ExpectedRestockDate));
        }

        return Task.FromResult(result);
    }
}
