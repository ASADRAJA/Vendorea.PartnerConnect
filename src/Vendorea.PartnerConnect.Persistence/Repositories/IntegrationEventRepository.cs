using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities.Integration;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class IntegrationEventRepository : IIntegrationEventRepository
{
    private readonly PartnerConnectDbContext _context;

    public IntegrationEventRepository(PartnerConnectDbContext context) => _context = context;

    /// <inheritdoc />
    public void Stage(IntegrationEvent integrationEvent)
        => _context.IntegrationEvents.Add(integrationEvent);

    public Task<IntegrationEvent?> GetAsync(Guid eventId, CancellationToken cancellationToken = default)
        => _context.IntegrationEvents.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);

    public async Task<IReadOnlyList<IntegrationEvent>> GetForOrderAsync(
        Guid salesOrderId, CancellationToken cancellationToken = default)
        => await _context.IntegrationEvents
            .AsNoTracking()
            .Where(e => e.SalesOrderId == salesOrderId)
            .OrderByDescending(e => e.PublishedAt)
            .ThenBy(e => e.EventId)
            .ToListAsync(cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
