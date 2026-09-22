using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

/// <summary>
/// As empresas assinantes. Não passa pelo filtro por empresa: a tabela de
/// empresas é justamente o que se consulta para DESCOBRIR a empresa (pelo slug,
/// no login) e o que o Criador lista inteira.
/// </summary>
public interface ITenantRepository
{
    Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default);
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tenant>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Várias empresas de uma vez — para rotular uma lista de chamados sem uma consulta por linha.</summary>
    Task<IReadOnlyList<Tenant>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
