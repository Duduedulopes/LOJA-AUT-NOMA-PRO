using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

public interface ICustomerRepository
{
    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Customer?> GetByCpfAsync(string cpf, CancellationToken cancellationToken = default);
    Task<Customer?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Compradores para a visão de quem opera a plataforma. Sem <paramref name="empresaId"/>,
    /// traz de todas as empresas — por isso só serve a contextos que veem todas.
    /// </summary>
    Task<IReadOnlyList<Customer>> ListarAsync(
        Guid? empresaId, string? busca, int limite, CancellationToken cancellationToken = default);

    /// <summary>
    /// O e-mail ou o CPF já estão cadastrados NESTA empresa? A pergunta tem de nomear a
    /// empresa: para quem vê todas, "existe alguém com este e-mail?" acharia a mesma pessoa
    /// em outra empresa e recusaria um cadastro que é perfeitamente válido.
    /// </summary>
    Task<bool> ExisteNaEmpresaAsync(
        Guid empresaId, string email, string cpf, CancellationToken cancellationToken = default);

    /// <summary>
    /// Quantos compradores cada empresa tem. Feito para quem enxerga TODAS as empresas (o painel do Criador): uma empresa sem
    /// nenhum comprador simplesmente não aparece no resultado.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> ContarPorEmpresaAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
