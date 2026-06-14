using Microsoft.Extensions.Logging;
using Vendorea.PartnerConnect.Contracts.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.PartnerAdapters.Common;

/// <summary>
/// Base class for partner adapters providing common functionality.
/// </summary>
public abstract class BasePartnerAdapter : IPartnerAdapter
{
    protected readonly ILogger Logger;

    protected BasePartnerAdapter(ILogger logger)
    {
        Logger = logger;
    }

    public abstract string PartnerCode { get; }

    public abstract IReadOnlyList<PartnerCapability> SupportedCapabilities { get; }

    public abstract Task<bool> TestConnectionAsync(TradingPartner partner, CancellationToken cancellationToken = default);

    protected void LogInfo(string message, params object[] args) => Logger.LogInformation(message, args);

    protected void LogWarning(string message, params object[] args) => Logger.LogWarning(message, args);

    protected void LogError(Exception ex, string message, params object[] args) => Logger.LogError(ex, message, args);
}
