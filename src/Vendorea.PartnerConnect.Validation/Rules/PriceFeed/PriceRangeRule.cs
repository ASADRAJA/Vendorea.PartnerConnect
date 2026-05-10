using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Validation.Core;
using Vendorea.PartnerConnect.Validation.Interfaces;

namespace Vendorea.PartnerConnect.Validation.Rules.PriceFeed;

/// <summary>
/// Validates that prices are within acceptable ranges and relationships.
/// </summary>
public class PriceRangeRule : ValidationRuleBase<PriceUpdate>
{
    private const decimal MaxReasonablePrice = 1_000_000m;
    private const decimal MinPositivePrice = 0.01m;

    public override string RuleCode => "PRICE_RANGE";
    public override string Description => "Validates that prices are within acceptable ranges";

    public override Task<ValidationResult> ValidateAsync(
        PriceUpdate item,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        var result = new ValidationResult();

        // Cost validation
        if (item.Cost > MaxReasonablePrice)
        {
            result.AddError(RuleCode,
                $"Cost {item.Cost} exceeds maximum reasonable price of {MaxReasonablePrice}",
                nameof(item.Cost));
        }

        if (item.Cost > 0 && item.Cost < MinPositivePrice)
        {
            result.AddWarning(RuleCode,
                $"Cost {item.Cost} is below {MinPositivePrice} - verify this is correct",
                nameof(item.Cost));
        }

        // List price validation
        if (item.ListPrice.HasValue)
        {
            if (item.ListPrice.Value < 0)
            {
                result.AddError(RuleCode, "ListPrice cannot be negative", nameof(item.ListPrice));
            }

            if (item.ListPrice.Value > MaxReasonablePrice)
            {
                result.AddError(RuleCode,
                    $"ListPrice {item.ListPrice.Value} exceeds maximum reasonable price",
                    nameof(item.ListPrice));
            }

            // Cost should typically be less than list price
            if (item.Cost > 0 && item.ListPrice.Value > 0 && item.Cost > item.ListPrice.Value)
            {
                result.AddWarning(RuleCode,
                    $"Cost ({item.Cost}) is greater than ListPrice ({item.ListPrice.Value})",
                    nameof(item.Cost));
            }
        }

        // MAP price validation
        if (item.MapPrice.HasValue)
        {
            if (item.MapPrice.Value < 0)
            {
                result.AddError(RuleCode, "MapPrice cannot be negative", nameof(item.MapPrice));
            }

            // MAP should typically be between cost and list price
            if (item.MapPrice.Value > 0 && item.ListPrice.HasValue &&
                item.MapPrice.Value > item.ListPrice.Value)
            {
                result.AddWarning(RuleCode,
                    $"MapPrice ({item.MapPrice.Value}) is greater than ListPrice ({item.ListPrice.Value})",
                    nameof(item.MapPrice));
            }

            if (item.MapPrice.Value > 0 && item.Cost > item.MapPrice.Value)
            {
                result.AddWarning(RuleCode,
                    $"Cost ({item.Cost}) is greater than MapPrice ({item.MapPrice.Value})",
                    nameof(item.MapPrice));
            }
        }

        // Price break validation
        if (item.PriceBreaks != null && item.PriceBreaks.Count > 0)
        {
            ValidatePriceBreaks(item.PriceBreaks, result);
        }

        return Task.FromResult(result);
    }

    private void ValidatePriceBreaks(IReadOnlyList<PriceBreak> priceBreaks, ValidationResult result)
    {
        var sortedBreaks = priceBreaks.OrderBy(pb => pb.MinQuantity).ToList();

        for (int i = 0; i < sortedBreaks.Count; i++)
        {
            var current = sortedBreaks[i];

            if (current.MinQuantity < 1)
            {
                result.AddError(RuleCode,
                    $"Price break minimum quantity must be at least 1",
                    $"PriceBreaks[{i}].MinQuantity");
            }

            if (current.UnitPrice < 0)
            {
                result.AddError(RuleCode,
                    $"Price break unit price cannot be negative",
                    $"PriceBreaks[{i}].UnitPrice");
            }

            if (current.MaxQuantity.HasValue && current.MaxQuantity.Value < current.MinQuantity)
            {
                result.AddError(RuleCode,
                    $"Price break max quantity cannot be less than min quantity",
                    $"PriceBreaks[{i}].MaxQuantity");
            }

            // Check for overlapping ranges
            if (i > 0)
            {
                var previous = sortedBreaks[i - 1];
                if (previous.MaxQuantity.HasValue && current.MinQuantity <= previous.MaxQuantity.Value)
                {
                    result.AddWarning(RuleCode,
                        $"Price break ranges may overlap at quantity {current.MinQuantity}",
                        $"PriceBreaks[{i}]");
                }

                // Typically, higher quantities should have lower unit prices
                if (current.UnitPrice >= previous.UnitPrice)
                {
                    result.AddWarning(RuleCode,
                        $"Price break at quantity {current.MinQuantity} has same or higher price than previous break",
                        $"PriceBreaks[{i}].UnitPrice");
                }
            }
        }
    }
}
