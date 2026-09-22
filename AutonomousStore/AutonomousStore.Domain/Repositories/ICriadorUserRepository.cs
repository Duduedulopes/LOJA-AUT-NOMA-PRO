using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

public interface ICriadorUserRepository
{
    Task AddAsync(CriadorUser criador, CancellationToken cancellationToken = default);
    Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default);
    Task<CriadorUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<CriadorUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
