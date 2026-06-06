using System.Text.Json;
using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Contracts.Integration;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Services;

/// <summary>
/// Resolves partner-specific order requirements from canonical data.
/// Maps business options to partner-specific XML/EDI requirements.
/// </summary>
public class PartnerOrderResolutionService : IPartnerOrderResolutionService
{
    private readonly ITenantPartnerAccountRepository _accountRepository;
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly ILogger<PartnerOrderResolutionService> _logger;

    public PartnerOrderResolutionService(
        ITenantPartnerAccountRepository accountRepository,
        ITradingPartnerRepository partnerRepository,
        ILogger<PartnerOrderResolutionService> logger)
    {
        _accountRepository = accountRepository;
        _partnerRepository = partnerRepository;
        _logger = logger;
    }

    public async Task<PartnerResolutionResult> ValidatePartnerConnectionAsync(
        int partnerConnectionId,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(partnerConnectionId, cancellationToken);

        if (account == null)
        {
            return PartnerResolutionResult.Failed(
                "PARTNER_CONNECTION_NOT_FOUND",
                "PartnerConnectionId",
                $"Partner connection {partnerConnectionId} not found");
        }

        if (!account.IsActive)
        {
            return PartnerResolutionResult.Failed(
                "PARTNER_CONNECTION_INACTIVE",
                "PartnerConnectionId",
                "Partner connection is inactive");
        }

        var partner = await _partnerRepository.GetByIdAsync(account.TradingPartnerId, cancellationToken);

        if (partner == null)
        {
            return PartnerResolutionResult.Failed(
                "TRADING_PARTNER_NOT_FOUND",
                "TradingPartnerId",
                "Trading partner not found");
        }

        if (partner.Status != TradingPartnerStatus.Active)
        {
            return PartnerResolutionResult.Failed(
                "TRADING_PARTNER_INACTIVE",
                "TradingPartnerId",
                $"Trading partner {partner.Code} is not active");
        }

        // Check that partner supports order submission
        var hasOrderCapability = partner.Capabilities?
            .Any(c => c.Capability == PartnerCapability.OrderSubmission && c.IsEnabled) ?? false;

        var warnings = new List<string>();
        if (!hasOrderCapability)
        {
            warnings.Add("Partner does not have OrderSubmission capability explicitly enabled");
        }

        // Resolve configuration
        var config = ResolveConfiguration(account, partner);

        return PartnerResolutionResult.Succeeded(account, partner, config, warnings);
    }

    public async Task<PartnerResolutionResult> ResolvePartnerRequirementsAsync(
        SubmitSupplierOrderRequest request,
        TenantPartnerAccount account,
        CancellationToken cancellationToken = default)
    {
        var partner = await _partnerRepository.GetByIdAsync(account.TradingPartnerId, cancellationToken);

        if (partner == null)
        {
            return PartnerResolutionResult.Failed(
                "TRADING_PARTNER_NOT_FOUND",
                "TradingPartnerId",
                "Trading partner not found");
        }

        var config = ResolveConfiguration(account, partner);

        // Validate partner-specific requirements are met
        var errors = ValidatePartnerRequirements(request, partner.Code, config);
        if (errors.Count > 0)
        {
            return PartnerResolutionResult.Failed(errors);
        }

        // Apply business options to partner config
        config = config with
        {
            // Map business option to partner-specific flag
            RequireCompleteShipment = !request.AllowPartialShipment
        };

        var warnings = new List<string>();

        // Validate downstream generation prerequisites
        if (partner.Code == "SPR")
        {
            if (string.IsNullOrWhiteSpace(config.EnterpriseCode))
            {
                warnings.Add("SPR EnterpriseCode not configured; using default");
            }
            if (string.IsNullOrWhiteSpace(config.BuyerOrgCode))
            {
                warnings.Add("SPR BuyerOrgCode not configured; will use AccountNumber");
            }
        }

        _logger.LogInformation(
            "Resolved partner requirements for {PartnerCode} account {AccountNumber}",
            partner.Code, account.AccountNumber);

        return PartnerResolutionResult.Succeeded(account, partner, config, warnings);
    }

    private PartnerOrderConfiguration ResolveConfiguration(
        TenantPartnerAccount account,
        TradingPartner partner)
    {
        // Parse partner-specific configuration from JSON
        var partnerConfig = ParsePartnerConfig(account.ConfigurationJson, partner.Code);

        return new PartnerOrderConfiguration
        {
            PartnerCode = partner.Code,
            AccountNumber = account.AccountNumber,
            EnterpriseCode = partnerConfig.GetValueOrDefault("EnterpriseCode"),
            BuyerOrgCode = partnerConfig.GetValueOrDefault("BuyerOrgCode") ?? account.AccountNumber,
            SellerOrgCode = partnerConfig.GetValueOrDefault("SellerOrgCode") ?? partner.Code,
            ShipNode = partnerConfig.GetValueOrDefault("ShipNode"),
            TransportType = partnerConfig.GetValueOrDefault("TransportType") ?? "SFTP",
            OutboundPath = partnerConfig.GetValueOrDefault("EdiOutboundPath") ?? "/edi/outbound",
            AutoSend997 = partnerConfig.GetValueOrDefault("AutoSend997") != "false"
        };
    }

    private Dictionary<string, string> ParsePartnerConfig(string? configJson, string partnerCode)
    {
        if (string.IsNullOrWhiteSpace(configJson))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                configJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (config == null)
            {
                return new Dictionary<string, string>();
            }

            // Flatten to string dictionary
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in config)
            {
                result[kvp.Key] = kvp.Value.ValueKind == JsonValueKind.String
                    ? kvp.Value.GetString() ?? string.Empty
                    : kvp.Value.ToString();
            }

            return result;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse partner configuration for {PartnerCode}",
                partnerCode);
            return new Dictionary<string, string>();
        }
    }

    private static List<ValidationError> ValidatePartnerRequirements(
        SubmitSupplierOrderRequest request,
        string partnerCode,
        PartnerOrderConfiguration config)
    {
        var errors = new List<ValidationError>();

        // Partner-specific validations
        switch (partnerCode.ToUpperInvariant())
        {
            case "SPR":
                // SPR requires ship-to address for all orders
                if (request.ShipTo == null)
                {
                    errors.Add(new ValidationError(
                        "SPR_REQUIRES_SHIPTO",
                        "ShipTo",
                        "SPR orders require a ship-to address"));
                }
                else
                {
                    // SPR requires specific address fields
                    if (string.IsNullOrWhiteSpace(request.ShipTo.Address1))
                    {
                        errors.Add(new ValidationError(
                            "SPR_REQUIRES_ADDRESS1",
                            "ShipTo.Address1",
                            "SPR orders require ship-to address line 1"));
                    }
                    if (string.IsNullOrWhiteSpace(request.ShipTo.City))
                    {
                        errors.Add(new ValidationError(
                            "SPR_REQUIRES_CITY",
                            "ShipTo.City",
                            "SPR orders require ship-to city"));
                    }
                    if (string.IsNullOrWhiteSpace(request.ShipTo.State))
                    {
                        errors.Add(new ValidationError(
                            "SPR_REQUIRES_STATE",
                            "ShipTo.State",
                            "SPR orders require ship-to state"));
                    }
                    if (string.IsNullOrWhiteSpace(request.ShipTo.PostalCode))
                    {
                        errors.Add(new ValidationError(
                            "SPR_REQUIRES_POSTALCODE",
                            "ShipTo.PostalCode",
                            "SPR orders require ship-to postal code"));
                    }
                }

                // SPR requires VendorSku (PartnerSku) on all lines
                for (int i = 0; i < request.Lines.Count; i++)
                {
                    var line = request.Lines[i];
                    if (string.IsNullOrWhiteSpace(line.VendorSku))
                    {
                        errors.Add(new ValidationError(
                            "SPR_REQUIRES_VENDOR_SKU",
                            $"Lines[{i}].VendorSku",
                            $"SPR orders require VendorSku on line {i + 1}"));
                    }
                }
                break;

            // Add other partner-specific validations here
            default:
                // Generic partner - just ensure basic requirements
                break;
        }

        return errors;
    }
}
