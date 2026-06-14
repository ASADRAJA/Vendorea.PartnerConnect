using Microsoft.EntityFrameworkCore;
using Vendorea.PartnerConnect.Application.Interfaces;
using Vendorea.PartnerConnect.Domain.Entities;

namespace Vendorea.PartnerConnect.Persistence.Repositories;

public class EdiDocumentRepository : IEdiDocumentRepository
{
    private readonly PartnerConnectDbContext _context;

    public EdiDocumentRepository(PartnerConnectDbContext context)
    {
        _context = context;
    }

    public async Task<EdiDocument?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.EdiDocuments
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<EdiDocument?> GetByIdWithRelationsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.EdiDocuments
            .Include(e => e.PartnerDocument)
            .Include(e => e.ResponseDocument)
            .Include(e => e.OriginalDocument)
            .Include(e => e.Responses)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<EdiDocument>> GetByPartnerDocumentIdAsync(
        int partnerDocumentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EdiDocuments
            .Where(e => e.PartnerDocumentId == partnerDocumentId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EdiDocument>> GetByTradingPartnerAsync(
        int tradingPartnerId,
        string? transactionSetCode = null,
        EdiDirection? direction = null,
        int skip = 0,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EdiDocuments
            .Include(e => e.PartnerDocument)
            .Where(e => e.PartnerDocument!.TradingPartnerId == tradingPartnerId);

        if (!string.IsNullOrEmpty(transactionSetCode))
        {
            query = query.Where(e => e.TransactionSetCode == transactionSetCode);
        }

        if (direction.HasValue)
        {
            query = query.Where(e => e.Direction == direction.Value);
        }

        return await query
            .OrderByDescending(e => e.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EdiDocument>> GetPendingOutboundAsync(
        int? tradingPartnerId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.EdiDocuments
            .Include(e => e.PartnerDocument)
            .Where(e => e.Direction == EdiDirection.Outbound)
            .Where(e => !e.AcknowledgmentSent);

        if (tradingPartnerId.HasValue)
        {
            query = query.Where(e => e.PartnerDocument!.TradingPartnerId == tradingPartnerId.Value);
        }

        return await query
            .OrderBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EdiDocument>> GetByInterchangeControlNumberAsync(
        string interchangeControlNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.EdiDocuments
            .Where(e => e.InterchangeControlNumber == interchangeControlNumber)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string interchangeControlNumber,
        string groupControlNumber,
        string transactionControlNumber,
        CancellationToken cancellationToken = default)
    {
        return await _context.EdiDocuments
            .AnyAsync(e =>
                e.InterchangeControlNumber == interchangeControlNumber &&
                e.GroupControlNumber == groupControlNumber &&
                e.TransactionControlNumber == transactionControlNumber,
                cancellationToken);
    }

    public async Task<EdiDocument> AddAsync(EdiDocument document, CancellationToken cancellationToken = default)
    {
        _context.EdiDocuments.Add(document);
        await _context.SaveChangesAsync(cancellationToken);
        return document;
    }

    public async Task UpdateAsync(EdiDocument document, CancellationToken cancellationToken = default)
    {
        document.UpdatedAt = DateTime.UtcNow;
        _context.EdiDocuments.Update(document);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var document = await GetByIdAsync(id, cancellationToken);
        if (document != null)
        {
            _context.EdiDocuments.Remove(document);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<Dictionary<string, int>> GetCountsByTransactionSetAsync(
        int tradingPartnerId,
        CancellationToken cancellationToken = default)
    {
        return await _context.EdiDocuments
            .Include(e => e.PartnerDocument)
            .Where(e => e.PartnerDocument!.TradingPartnerId == tradingPartnerId)
            .GroupBy(e => e.TransactionSetCode)
            .ToDictionaryAsync(
                g => g.Key,
                g => g.Count(),
                cancellationToken);
    }

    public async Task<int> GetNextControlNumberAsync(
        int tradingPartnerId,
        string controlNumberType,
        CancellationToken cancellationToken = default)
    {
        // Select the appropriate control number column based on type
        IQueryable<string?> query = controlNumberType switch
        {
            "ISA" => _context.EdiDocuments
                .Include(e => e.PartnerDocument)
                .Where(e => e.PartnerDocument!.TradingPartnerId == tradingPartnerId)
                .Where(e => e.Direction == EdiDirection.Outbound)
                .Select(e => e.InterchangeControlNumber),
            "GS" => _context.EdiDocuments
                .Include(e => e.PartnerDocument)
                .Where(e => e.PartnerDocument!.TradingPartnerId == tradingPartnerId)
                .Where(e => e.Direction == EdiDirection.Outbound)
                .Select(e => e.GroupControlNumber),
            "ST" => _context.EdiDocuments
                .Include(e => e.PartnerDocument)
                .Where(e => e.PartnerDocument!.TradingPartnerId == tradingPartnerId)
                .Where(e => e.Direction == EdiDirection.Outbound)
                .Select(e => e.TransactionControlNumber),
            _ => _context.EdiDocuments
                .Include(e => e.PartnerDocument)
                .Where(e => e.PartnerDocument!.TradingPartnerId == tradingPartnerId)
                .Where(e => e.Direction == EdiDirection.Outbound)
                .Select(e => e.InterchangeControlNumber)
        };

        // Fetch to memory and parse
        var controlNumbers = await query
            .Where(n => n != null)
            .ToListAsync(cancellationToken);

        var maxNumber = controlNumbers
            .Select(n => int.TryParse(n, out var num) ? num : 0)
            .DefaultIfEmpty(0)
            .Max();

        return maxNumber + 1;
    }
}
