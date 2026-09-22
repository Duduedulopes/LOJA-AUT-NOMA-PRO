using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

/// <summary>
/// As lojas de uma empresa. O limite de lojas da assinatura conta as lojas ATIVAS: desativar uma loja libera a vaga, e por
/// isso reativá-la volta a passar pela checagem do limite.
/// </summary>
public interface IStoreRepository
{
    Task AddAsync(Store loja, CancellationToken cancellationToken = default);
    Task<Store?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>As lojas que o contexto enxerga: para o Admin, as da empresa dele; para quem opera a plataforma, todas.</summary>
    Task<IReadOnlyList<Store>> ListarAsync(CancellationToken cancellationToken = default);

    /// <summary>As lojas de UMA empresa — para quem vê todas e precisa olhar uma (o Criador).</summary>
    Task<IReadOnlyList<Store>> ListarDaEmpresaAsync(Guid empresaId, CancellationToken cancellationToken = default);

    /// <summary>Quantas lojas ativas a empresa tem. É o número que o limite da assinatura confere.</summary>
    Task<int> ContarAtivasAsync(Guid empresaId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Já existe, nesta empresa, uma loja com este nome (sem diferenciar maiúscula)? A pergunta nomeia a empresa: para
    /// quem vê todas, "existe uma loja com este nome?" acharia a de OUTRA empresa e recusaria um nome perfeitamente válido.
    /// </summary>
    Task<bool> ExisteComNomeAsync(Guid empresaId, string nome, Guid? exceto = null, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
