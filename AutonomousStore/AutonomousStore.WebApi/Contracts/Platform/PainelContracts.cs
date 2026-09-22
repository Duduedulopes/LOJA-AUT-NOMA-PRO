using AutonomousStore.Domain.Enums;

namespace AutonomousStore.WebApi.Contracts.Platform;

/// <summary>Tudo o que o painel do Criador mostra, numa resposta só.</summary>
public record PainelResponse(
    ContagemDeEmpresas Empresas,
    ContagemDeLojas Lojas,
    int Tecnicos,
    int Compradores,
    ResumoDoSuporte Suporte,
    IReadOnlyList<EmpresaNoPainel> PorEmpresa);

public record ContagemDeEmpresas(int Total, int Ativas, int Suspensas);

/// <param name="Ativas">Lojas ativas das empresas ATIVAS.</param>
/// <param name="Limite">A soma dos limites das empresas ativas: quanto a assinatura vigente permite.</param>
public record ContagemDeLojas(int Ativas, int Limite);

/// <param name="NaFila">Chamados que esperam o suporte agora.</param>
/// <param name="Graves">Dos da fila, os de severidade Alta ou Crítica.</param>
/// <param name="HorasDoMaisAntigo">Há quantas horas espera o chamado mais antigo da fila (nulo com a fila vazia).</param>
/// <param name="Resolvidas30Dias">Resolvidas nos últimos 30 dias (as ignoradas não contam).</param>
/// <param name="HorasMediasParaResolver">Média de horas entre o chamado nascer e ser resolvido (nulo sem nenhuma resolvida).</param>
public record ResumoDoSuporte(
    int NaFila,
    int Graves,
    double? HorasDoMaisAntigo,
    int Resolvidas30Dias,
    double? HorasMediasParaResolver,
    IReadOnlyList<ResolvidasPor> PorQuemResolveu);

public record ResolvidasPor(string Nome, int Quantidade, double HorasMedias);

/// <param name="Rotulo">O que o técnico também vê: "0002 · Rede Sabor".</param>
public record EmpresaNoPainel(
    Guid Id,
    int Codigo,
    string Rotulo,
    string Slug,
    StatusDoTenant Status,
    int LimiteDeLojas,
    int LojasAtivas,
    int Compradores,
    int NaFila);

public record TecnicoResponse(Guid Id, string Nome, string Email, bool Ativo, DateTime CriadoEm);
