using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class TenantRepository : ITenantRepository
{
    private readonly AutonomousDbContext _context;

    public TenantRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
    {
        // O proximo numero livre. Duas empresas cadastradas no MESMO instante
        // pegariam o mesmo numero — mas o indice unico do banco barra a segunda, e
        // quem cadastra empresas e uma pessoa so, uma de cada vez.
        var maior = await _context.Tenants.MaxAsync(t => (int?)t.Codigo, cancellationToken) ?? 0;
        tenant.AtribuirCodigo(maior + 1);

        await _context.Tenants.AddAsync(tenant, cancellationToken);
    }

    public async Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var alvo = (slug ?? "").Trim().ToLowerInvariant();

        return await _context.Tenants
            .FirstOrDefaultAsync(t => t.Slug == alvo, cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Tenants
            .AsNoTracking()
            .OrderBy(t => t.Nome)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Tenant>> GetByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        var alvo = ids.Distinct().ToList();
        if (alvo.Count == 0) return Array.Empty<Tenant>();

        return await _context.Tenants
            .AsNoTracking()
            .Where(t => alvo.Contains(t.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
