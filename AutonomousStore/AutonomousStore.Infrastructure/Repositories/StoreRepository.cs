using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class StoreRepository : IStoreRepository
{
    private readonly AutonomousDbContext _context;

    public StoreRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Store loja, CancellationToken cancellationToken = default)
    {
        await _context.Stores.AddAsync(loja, cancellationToken);
    }

    public async Task<Store?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Stores
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Store>> ListarAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Stores
            .AsNoTracking()
            .OrderBy(s => s.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Store>> ListarDaEmpresaAsync(Guid empresaId, CancellationToken cancellationToken = default)
    {
        return await _context.Stores
            .AsNoTracking()
            .Where(s => s.TenantId == empresaId)
            .OrderBy(s => s.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> ContarAtivasAsync(Guid empresaId, CancellationToken cancellationToken = default)
    {
        return await _context.Stores
            .CountAsync(s => s.TenantId == empresaId && s.IsActive, cancellationToken);
    }

    public async Task<bool> ExisteComNomeAsync(
        Guid empresaId, string nome, Guid? exceto = null, CancellationToken cancellationToken = default)
    {
        var alvo = (nome ?? "").Trim().ToLower();

        return await _context.Stores
            .AsNoTracking()
            .AnyAsync(
                s => s.TenantId == empresaId && s.Nome.ToLower() == alvo && s.Id != exceto,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
