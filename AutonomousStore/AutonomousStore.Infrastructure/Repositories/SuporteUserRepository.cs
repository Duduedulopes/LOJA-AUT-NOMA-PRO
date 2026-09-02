using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class SuporteUserRepository : ISuporteUserRepository
{
    private readonly AutonomousDbContext _context;

    public SuporteUserRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SuporteUser suporte, CancellationToken cancellationToken = default)
    {
        await _context.AddAsync(suporte, cancellationToken);
    }

    public async Task<SuporteUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SuporteUser>()
            .FirstOrDefaultAsync(s => s.Email == email, cancellationToken);
    }

    public async Task<SuporteUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<SuporteUser>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Set<SuporteUser>().AnyAsync(cancellationToken);
    }

}
