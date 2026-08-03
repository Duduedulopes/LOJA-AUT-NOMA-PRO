using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class AdminUserRepository : IAdminUserRepository
{
    private readonly AutonomousDbContext _context;

    public AdminUserRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AdminUser admin, CancellationToken cancellationToken = default)
    {
        await _context.AdminUsers.AddAsync(admin, cancellationToken);
    }

    public async Task<AdminUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.AdminUsers
            .FirstOrDefaultAsync(a => a.Email == email, cancellationToken);
    }

    public async Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AdminUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
