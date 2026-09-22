using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class AuditoriaRepository : IAuditoriaRepository
{
    private readonly AutonomousDbContext _context;

    public AuditoriaRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Auditoria auditoria, CancellationToken cancellationToken = default)
    {
        await _context.Auditorias.AddAsync(auditoria, cancellationToken);
    }

    public async Task<IReadOnlyList<Auditoria>> BuscarAsync(
        FiltroDeAuditoria filtro, CancellationToken cancellationToken = default)
    {
        var q = _context.Auditorias.AsNoTracking().AsQueryable();

        if (filtro.Desde is { } desde) q = q.Where(a => a.QuandoUtc >= desde);
        if (filtro.Ate is { } ate) q = q.Where(a => a.QuandoUtc < ate);
        if (filtro.TenantAlvoId is { } tenant) q = q.Where(a => a.TenantAlvoId == tenant);
        if (filtro.AtorId is { } ator) q = q.Where(a => a.AtorId == ator);
        if (!string.IsNullOrWhiteSpace(filtro.Acao)) q = q.Where(a => a.Acao == filtro.Acao);

        return await q
            .OrderByDescending(a => a.QuandoUtc)
            .Take(Math.Clamp(filtro.Limite, 1, 1000))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
