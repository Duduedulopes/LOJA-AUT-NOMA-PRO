using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class StoreSessionRepository : IStoreSessionRepository
{
    private readonly AutonomousDbContext _context;

    public StoreSessionRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(StoreSession session, CancellationToken cancellationToken = default)
    {
        await _context.StoreSessions.AddAsync(session, cancellationToken);
    }

    public async Task<StoreSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.StoreSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<StoreSession?> GetByQrCodeTokenAsync(string qrCodeToken, CancellationToken cancellationToken = default)
    {
        return await _context.StoreSessions
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.QrCodeToken == qrCodeToken, cancellationToken);
    }

    public async Task<StoreSession?> GetActiveSessionByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.StoreSessions
            .Include(s => s.Items)
            .Where(s => s.CustomerId == customerId)
            .Where(s => s.Status == SessionStatus.AguardandoEntrada || s.Status == SessionStatus.Aberta)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<StoreSession?> GetCurrentOpenSessionAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StoreSessions
            .Include(s => s.Items)
            .Where(s => s.Status == SessionStatus.Aberta)
            .OrderByDescending(s => s.EntryConfirmedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoreSession>> GetPendingEntrySessionsAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StoreSessions
            .Where(s => s.Status == SessionStatus.AguardandoEntrada)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoreSession>> GetHistoryAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StoreSessions
            .Include(s => s.Items)
            .Where(s => s.Status == SessionStatus.Concluida)
            .OrderByDescending(s => s.PaymentConfirmedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StoreSession>> GetHistoryByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await _context.StoreSessions
            .Include(s => s.Items)
            .Where(s => s.CustomerId == customerId && s.Status == SessionStatus.Concluida)
            .OrderByDescending(s => s.PaymentConfirmedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<StoreSession?> GetMostRecentSessionAsync(CancellationToken cancellationToken = default)
    {
        return await _context.StoreSessions
            .Include(s => s.Items)
            .Where(s => s.Status != SessionStatus.Cancelada)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
