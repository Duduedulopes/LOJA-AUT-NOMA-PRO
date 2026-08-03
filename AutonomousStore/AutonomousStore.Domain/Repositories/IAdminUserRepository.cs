using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

public interface IAdminUserRepository
{
    Task AddAsync(AdminUser admin, CancellationToken cancellationToken = default);
    Task<AdminUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<AdminUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
