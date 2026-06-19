using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;
using Vendorea.PartnerConnect.PartnerAdapters.SPR;

namespace Vendorea.PartnerConnect.Api.Controllers.V1;

/// <summary>
/// Public API v1 controller for trading partner operations.
/// Used by dealers (API key auth) and Merchant360 (OAuth2) to get partner catalog.
/// </summary>
[ApiController]
[Route("api/v1/partners")]
[Vendorea.PartnerConnect.Api.Authorization.RequireScope(ApiScopes.PartnersRead)]
public class PublicTradingPartnersController : ControllerBase
{
    private readonly ITradingPartnerRepository _partnerRepository;
    private readonly IPriceFeedUploadRepository _priceFeedRepository;
    private readonly ISprContentUploadRepository _contentUploadRepository;
    private readonly ICredentialProtector _credentialProtector;
    private readonly ILogger<PublicTradingPartnersController> _logger;

    public PublicTradingPartnersController(
        ITradingPartnerRepository partnerRepository,
        IPriceFeedUploadRepository priceFeedRepository,
        ISprContentUploadRepository contentUploadRepository,
        ICredentialProtector credentialProtector,
        ILogger<PublicTradingPartnersController> logger)
    {
        _partnerRepository = partnerRepository;
        _priceFeedRepository = priceFeedRepository;
        _contentUploadRepository = contentUploadRepository;
        _credentialProtector = credentialProtector;
        _logger = logger;
    }

    /// <summary>
    /// Gets available trading partners with data availability info.
    /// Used by Merchant360 to display partner catalog for subscriptions.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAvailablePartners(CancellationToken cancellationToken)
    {
        var partners = await _partnerRepository.GetAllAsync(cancellationToken);
        var activePartners = partners.Where(p => p.Status == TradingPartnerStatus.Active).ToList();

        var result = new List<object>();
        foreach (var p in activePartners)
        {
            var hasPriceData = await _priceFeedRepository.HasDataForPartnerAsync(p.Id, cancellationToken);
            var hasEnhancedContent = await _contentUploadRepository.HasDataForPartnerAsync(p.Id, cancellationToken);

            result.Add(new
            {
                p.Id,
                p.Code,
                p.Name,
                p.Description,
                p.LogoUrl,
                Status = p.Status.ToString(),
                HasPriceData = hasPriceData,
                HasEnhancedContent = hasEnhancedContent,
                IsActive = p.Status == TradingPartnerStatus.Active,
                ConnectionRequirements = ParseRequirements(p.TenantConfirmationFieldsJson)
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Gets a specific trading partner with data availability info.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetPartner(int id, CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByIdAsync(id, cancellationToken);

        if (partner == null)
        {
            return NotFound();
        }

        var hasPriceData = await _priceFeedRepository.HasDataForPartnerAsync(partner.Id, cancellationToken);
        var hasEnhancedContent = await _contentUploadRepository.HasDataForPartnerAsync(partner.Id, cancellationToken);

        return Ok(new
        {
            partner.Id,
            partner.Code,
            partner.Name,
            partner.Description,
            partner.LogoUrl,
            Status = partner.Status.ToString(),
            HasPriceData = hasPriceData,
            HasEnhancedContent = hasEnhancedContent,
            IsActive = partner.Status == TradingPartnerStatus.Active,
            ConnectionRequirements = ParseRequirements(partner.TenantConfirmationFieldsJson)
        });
    }

    /// <summary>
    /// Updates a trading partner's tenant connection requirements (the list of requirement names
    /// PC staff verify with the partner before approving a connection). Other partner fields are
    /// managed outside this app.
    /// </summary>
    [HttpPut("{id:int}/connection-requirements")]
    [Vendorea.PartnerConnect.Api.Authorization.RequireScope(ApiScopes.Admin)]
    public async Task<IActionResult> UpdateConnectionRequirements(
        int id,
        [FromBody] UpdateConnectionRequirementsRequest request,
        CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByIdAsync(id, cancellationToken);
        if (partner == null)
            return NotFound();

        var requirements = (request.Requirements ?? new List<string>())
            .Select(r => r?.Trim() ?? string.Empty)
            .Where(r => r.Length > 0)
            .ToList();

        partner.TenantConfirmationFieldsJson = requirements.Count > 0
            ? JsonSerializer.Serialize(requirements)
            : null;
        partner.UpdatedAt = DateTime.UtcNow;

        await _partnerRepository.UpdateAsync(partner, cancellationToken);
        _logger.LogInformation("Updated connection requirements for partner {PartnerId} ({Count} fields)", id, requirements.Count);

        return NoContent();
    }

    /// <summary>
    /// Returns a partner's transport configuration for read-only display in the admin portal.
    /// Non-secret settings (SFTP host/paths, EDI/SOAP identifiers) are returned as-is; secret
    /// values (SFTP password, key passphrase, SOAP password) are never returned — only a flag
    /// indicating whether each is configured. Shaped to the SPR config today (the only partner);
    /// generalize when more adapters are added.
    /// </summary>
    [HttpGet("{id:int}/transport")]
    public async Task<IActionResult> GetTransport(int id, CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByIdAsync(id, cancellationToken);
        if (partner == null)
            return NotFound();

        var config = SprConfiguration.FromJson(partner.TransportConfigJson);
        var credentials = SprCredentials.FromJson(_credentialProtector.Unprotect(partner.TransportCredentialsJson));

        var dto = new PartnerTransportDto
        {
            HasConfig = !string.IsNullOrWhiteSpace(partner.TransportConfigJson),

            // SFTP
            SftpHost = config.SftpHost,
            SftpPort = config.SftpPort,
            SftpUsername = config.SftpUsername,
            ConnectionTimeoutSeconds = config.ConnectionTimeoutSeconds,

            // Feeds
            PriceFeedPath = config.PriceFeedPath,
            InventoryFeedPath = config.InventoryFeedPath,
            ArchivePath = config.ArchivePath,
            PriceFeedFilePattern = config.PriceFeedFilePattern,
            InventoryFeedFilePattern = config.InventoryFeedFilePattern,
            DeleteAfterProcessing = config.DeleteAfterProcessing,
            ArchiveAfterProcessing = config.ArchiveAfterProcessing,
            CsvDelimiter = config.CsvDelimiter.ToString(),
            CsvHasHeader = config.CsvHasHeader,
            SprCustomerNumber = config.SprCustomerNumber,
            PricingTier = config.PricingTier.ToString(),

            // EDI
            EdiInboundPath = config.EdiInboundPath,
            EdiOutboundPath = config.EdiOutboundPath,
            EdiArchivePath = config.EdiArchivePath,
            EdiFilePattern = config.EdiFilePattern,
            AutoSend997 = config.AutoSend997,
            AutoSend855 = config.AutoSend855,
            EdiSyncIntervalMinutes = config.EdiSyncIntervalMinutes,
            IsaSenderQualifier = config.IsaSenderQualifier,
            IsaSenderId = config.IsaSenderId,
            IsaReceiverQualifier = config.IsaReceiverQualifier,
            IsaReceiverId = config.IsaReceiverId,
            GsApplicationSenderCode = config.GsApplicationSenderCode,
            GsApplicationReceiverCode = config.GsApplicationReceiverCode,

            // SOAP / XML order exchange
            SoapEndpointUrl = config.SoapEndpointUrl,
            SoapUsername = config.SoapUsername,
            EnterpriseCode = config.EnterpriseCode,
            BuyerOrgCode = config.BuyerOrgCode,
            SellerOrgCode = config.SellerOrgCode,
            SprXmlInboundPath = config.SprXmlInboundPath,
            SprXmlOutboundPath = config.SprXmlOutboundPath,
            SprXmlSftpPort = config.SprXmlSftpPort,
            SprXmlFilePattern = config.SprXmlFilePattern,
            UseSoapForOrders = config.UseSoapForOrders,
            SoapTimeoutSeconds = config.SoapTimeoutSeconds,

            // Secrets — presence only, never the value
            SftpPasswordConfigured = !string.IsNullOrWhiteSpace(credentials.SftpPassword),
            PrivateKeyConfigured = !string.IsNullOrWhiteSpace(credentials.PrivateKeyPath),
            PrivateKeyPassphraseConfigured = !string.IsNullOrWhiteSpace(credentials.PrivateKeyPassphrase),
            SoapPasswordConfigured = !string.IsNullOrWhiteSpace(config.SoapPassword),
        };

        return Ok(dto);
    }

    /// <summary>
    /// Sets a partner's SPR interactive web-services credentials (base URL, GroupCode, UserId, and
    /// the password — encrypted at rest). Merges into the existing transport config/credentials so
    /// other settings (SFTP, EDI, SOAP order exchange) are preserved. The password is never returned.
    /// </summary>
    [HttpPut("{id:int}/webservice-credentials")]
    [Vendorea.PartnerConnect.Api.Authorization.RequireScope(ApiScopes.Admin)]
    public async Task<IActionResult> SetWebServiceCredentials(
        int id, [FromBody] SetWebServiceCredentialsRequest request, CancellationToken cancellationToken)
    {
        var partner = await _partnerRepository.GetByIdAsync(id, cancellationToken);
        if (partner is null)
            return NotFound();

        var config = SprConfiguration.FromJson(partner.TransportConfigJson);
        config.WebServicesBaseUrl = string.IsNullOrWhiteSpace(request.BaseUrl) ? config.WebServicesBaseUrl : request.BaseUrl.Trim();
        config.WebServicesGroupCode = request.GroupCode?.Trim() ?? config.WebServicesGroupCode;
        config.WebServicesUserId = request.UserId?.Trim() ?? config.WebServicesUserId;
        if (request.TimeoutSeconds is > 0)
            config.WebServicesTimeoutSeconds = request.TimeoutSeconds.Value;
        partner.TransportConfigJson = config.ToJson();

        // Only overwrite the password when a new one is supplied; encrypt the whole creds blob.
        var credentials = SprCredentials.FromJson(_credentialProtector.Unprotect(partner.TransportCredentialsJson));
        if (!string.IsNullOrEmpty(request.Password))
            credentials.WebServicesPassword = request.Password;
        partner.TransportCredentialsJson = _credentialProtector.Protect(
            JsonSerializer.Serialize(credentials));

        partner.UpdatedAt = DateTime.UtcNow;
        await _partnerRepository.UpdateAsync(partner, cancellationToken);

        _logger.LogInformation("Updated SPR web-service credentials for partner {PartnerId}", id);
        return NoContent();
    }

    private static List<string> ParseRequirements(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    private int? GetDealerIdFromClaims()
    {
        var dealerIdClaim = User.FindFirst("DealerId")?.Value;
        if (int.TryParse(dealerIdClaim, out var dealerId))
        {
            return dealerId;
        }
        return null;
    }
}

/// <summary>Request to replace a trading partner's tenant connection requirement names.</summary>
public class UpdateConnectionRequirementsRequest
{
    public List<string>? Requirements { get; set; }
}

/// <summary>Request to set a partner's SPR interactive web-services credentials.</summary>
public class SetWebServiceCredentialsRequest
{
    public string? BaseUrl { get; set; }
    public string? GroupCode { get; set; }
    public string? UserId { get; set; }
    /// <summary>Plaintext password; encrypted before storage. Omit to leave the existing one unchanged.</summary>
    public string? Password { get; set; }
    public int? TimeoutSeconds { get; set; }
}

/// <summary>
/// Read-only view of a partner's transport configuration for the admin portal. Secret values
/// are represented by *Configured booleans only — the underlying secrets are never serialized.
/// </summary>
public class PartnerTransportDto
{
    /// <summary>True if the partner has any transport config persisted (vs. defaults).</summary>
    public bool HasConfig { get; set; }

    // SFTP connection
    public string SftpHost { get; set; } = string.Empty;
    public int SftpPort { get; set; }
    public string SftpUsername { get; set; } = string.Empty;
    public int ConnectionTimeoutSeconds { get; set; }

    // Price / inventory feeds
    public string PriceFeedPath { get; set; } = string.Empty;
    public string InventoryFeedPath { get; set; } = string.Empty;
    public string? ArchivePath { get; set; }
    public string PriceFeedFilePattern { get; set; } = string.Empty;
    public string InventoryFeedFilePattern { get; set; } = string.Empty;
    public bool DeleteAfterProcessing { get; set; }
    public bool ArchiveAfterProcessing { get; set; }
    public string CsvDelimiter { get; set; } = string.Empty;
    public bool CsvHasHeader { get; set; }
    public string? SprCustomerNumber { get; set; }
    public string PricingTier { get; set; } = string.Empty;

    // EDI (X12)
    public string EdiInboundPath { get; set; } = string.Empty;
    public string EdiOutboundPath { get; set; } = string.Empty;
    public string EdiArchivePath { get; set; } = string.Empty;
    public string EdiFilePattern { get; set; } = string.Empty;
    public bool AutoSend997 { get; set; }
    public bool AutoSend855 { get; set; }
    public int EdiSyncIntervalMinutes { get; set; }
    public string IsaSenderQualifier { get; set; } = string.Empty;
    public string IsaSenderId { get; set; } = string.Empty;
    public string IsaReceiverQualifier { get; set; } = string.Empty;
    public string IsaReceiverId { get; set; } = string.Empty;
    public string GsApplicationSenderCode { get; set; } = string.Empty;
    public string GsApplicationReceiverCode { get; set; } = string.Empty;

    // SOAP / XML order exchange
    public string? SoapEndpointUrl { get; set; }
    public string? SoapUsername { get; set; }
    public string? EnterpriseCode { get; set; }
    public string? BuyerOrgCode { get; set; }
    public string? SellerOrgCode { get; set; }
    public string SprXmlInboundPath { get; set; } = string.Empty;
    public string SprXmlOutboundPath { get; set; } = string.Empty;
    public int SprXmlSftpPort { get; set; }
    public string SprXmlFilePattern { get; set; } = string.Empty;
    public bool UseSoapForOrders { get; set; }
    public int SoapTimeoutSeconds { get; set; }

    // Secrets — presence flags only
    public bool SftpPasswordConfigured { get; set; }
    public bool PrivateKeyConfigured { get; set; }
    public bool PrivateKeyPassphraseConfigured { get; set; }
    public bool SoapPasswordConfigured { get; set; }
}
