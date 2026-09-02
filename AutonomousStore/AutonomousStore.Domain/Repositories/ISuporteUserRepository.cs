using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

public interface ISuporteUserRepository
{
    Task AddAsync(SuporteUser suporte, CancellationToken cancellationToken = default);

    /// <summary>Ja existe algum tecnico cadastrado?</summary>
    /// <remarks>
    /// E o que fecha a porta do cadastro depois do primeiro. Enquanto a
    /// tabela esta vazia, qualquer um pode criar o primeiro usuario — e
    /// nao ha nada a proteger, porque nao ha dado de suporte ainda. A
    /// partir do segundo, so quem ja e do suporte cria.
    /// </remarks>
    Task<bool> ExisteAlgumAsync(CancellationToken cancellationToken = default);
    Task<SuporteUser?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<SuporteUser?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
