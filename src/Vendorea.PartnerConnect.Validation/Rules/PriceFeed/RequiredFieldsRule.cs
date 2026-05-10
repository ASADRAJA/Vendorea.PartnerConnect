using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Validation.Core;
using Vendorea.PartnerConnect.Validation.Interfaces;

namespace Vendorea.PartnerConnect.Validation.Rules.PriceFeed;

/// <summary>
/// Validates that required fields are present on price updates.
/// </summary>
public class PriceRequiredFieldsRule : ValidationRuleBase<PriceUpdate>
{
    public override string RuleCode => "PRICE_REQUIRED_FIELDS";
    public override string Description => "Validates that all required fields are present";
    public override bool StopOnFailure => true;

    public override Task<ValidationResult> ValidateAsync(
        PriceUpdate item,
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

        if (item.Cost < 0)
        {
            result.AddError(RuleCode, "Cost cannot be negative", nameof(item.Cost));
        }

        // Either UPC or ManufacturerPartNumber should be present (warning if missing both)
        if (string.IsNullOrWhiteSpace(item.Upc) && string.IsNullOrWhiteSpace(item.ManufacturerPartNumber))
        {
            result.AddWarning(RuleCode, "Neither UPC nor ManufacturerPartNumber provided - matching may be less accurate");
        }

        return Task.FromResult(result);
    }
}
