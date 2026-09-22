using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

public record FiltroDeAuditoria(
    DateTime? Desde = null,
    DateTime? Ate = null,
    Guid? TenantAlvoId = null,
    Guid? AtorId = null,
    string? Acao = null,
    int Limite = 200);

/// <summary>
/// Só acrescenta e lê. Não existe "alterar" nem "apagar" aqui: quem pode
/// reescrever a auditoria a torna inútil.
/// </summary>
public interface IAuditoriaRepository
{
    Task AddAsync(Auditoria auditoria, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Auditoria>> BuscarAsync(FiltroDeAuditoria filtro, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
