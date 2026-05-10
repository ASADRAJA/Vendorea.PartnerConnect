using Microsoft.Extensions.DependencyInjection;
using Vendorea.PartnerConnect.Canonical.Models;
using Vendorea.PartnerConnect.Validation.Core;
using Vendorea.PartnerConnect.Validation.Interfaces;
using Vendorea.PartnerConnect.Validation.Rules.InventoryFeed;
using Vendorea.PartnerConnect.Validation.Rules.PriceFeed;

namespace Vendorea.PartnerConnect.Validation;

/// <summary>
/// Extension methods for registering validation services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds validation services to the service collection.
    /// </summary>
    public static IServiceCollection AddValidation(this IServiceCollection services)
    {
        // Register price feed validation rules
        services.AddTransient<IValidationRule<PriceUpdate>, PriceRequiredFieldsRule>();
        services.AddTransient<IValidationRule<PriceUpdate>, PriceRangeRule>();

        // Register inventory feed validation rules
        services.AddTransient<IValidationRule<InventoryUpdate>, InventoryRequiredFieldsRule>();
        services.AddTransient<IValidationRule<InventoryUpdate>, QuantityNonNegativeRule>();
        services.AddTransient<IValidationRule<InventoryUpdate>, AvailabilityStatusConsistencyRule>();

        // Register document validators
        services.AddTransient<IDocumentValidator<PriceUpdate>, DocumentValidator<PriceUpdate>>();
        services.AddTransient<IDocumentValidator<InventoryUpdate>, DocumentValidator<InventoryUpdate>>();

        return services;
    }
}
