namespace AutonomousStore.CriadorApp.Models;

// Espelham AutonomousStore.WebApi/Contracts/Platform/PainelContracts.cs — a tela inteira numa chamada só.
public record PainelDto(
    ContagemDeEmpresasDto Empresas,
    ContagemDeLojasDto Lojas,
    int Tecnicos,
    int Compradores,
    ResumoDoSuporteDto Suporte,
    List<EmpresaNoPainelDto> PorEmpresa);

public record ContagemDeEmpresasDto(int Total, int Ativas, int Suspensas);

public record ContagemDeLojasDto(int Ativas, int Limite);

public record ResumoDoSuporteDto(
    int NaFila,
    int Graves,
    double? HorasDoMaisAntigo,
    int Resolvidas30Dias,
    double? HorasMediasParaResolver,
    List<ResolvidasPorDto> PorQuemResolveu);

public record ResolvidasPorDto(string Nome, int Quantidade, double HorasMedias);

/// <param name="Status">"Ativa" ou "Suspensa".</param>
public record EmpresaNoPainelDto(
    Guid Id,
    int Codigo,
    string Rotulo,
    string Slug,
    string Status,
    int LimiteDeLojas,
    int LojasAtivas,
    int Compradores,
    int NaFila);
