using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Application.Interfaces;

/// <summary>
/// Applies a tenant's <see cref="OrderProcessingMode"/> to an order that has just been submitted.
/// </summary>
public interface IOrderAutoProcessingService
{
    /// <summary>
    /// Acknowledges the order and queues its transmission, if the owning tenant is set to
    /// Automatic. Returns true if it acted.
    /// </summary>
    /// <remarks>
    /// Never throws. An order that has been accepted must not be rejected because the step after
    /// acceptance failed — the caller has already told the submitter the order exists.
    /// </remarks>
    Task<bool> TryAutoProcessAsync(Order order, CancellationToken cancellationToken = default);
}
