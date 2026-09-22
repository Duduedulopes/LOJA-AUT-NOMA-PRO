using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class CriadorUserRepository : ICriadorUserRepository
{
    private readonly AutonomousDbContext _context;

    public CriadorUserRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(CriadorUser criador, CancellationToken cancellationToken = default)
    {
        await _context.CriadorUsers.AddAsync(criador, cancellationToken);
    }

    public async Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default)
    {
        return await _context.CriadorUsers.AnyAsync(cancellationToken);
    }

    public async Task<CriadorUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var alvo = (email ?? "").Trim().ToLower();

        return await _context.CriadorUsers
            .FirstOrDefaultAsync(c => c.Email.ToLower() == alvo, cancellationToken);
    }

    public async Task<CriadorUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CriadorUsers
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
