namespace AutonomousStore.WebApi.Contracts.Lojas;

public record LojaResponse(Guid Id, string Nome, string? Segmento, bool Ativa, DateTime CriadaEm);

/// <summary>
/// As lojas da empresa e o quanto da assinatura já foi usado: <see cref="Ativas"/> de <see cref="Limite"/>. A tela mostra os
/// dois números — é o que faz "não posso criar outra loja" deixar de ser uma surpresa.
/// </summary>
public record LojasResponse(IReadOnlyList<LojaResponse> Lojas, int Ativas, int Limite);

/// <param name="Segmento">O tipo de negócio ("Comida", "Roupa"). Texto livre, só descreve.</param>
public record SalvarLojaRequest(string Nome, string? Segmento);
