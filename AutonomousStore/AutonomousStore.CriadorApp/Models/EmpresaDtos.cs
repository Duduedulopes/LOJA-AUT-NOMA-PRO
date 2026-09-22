namespace AutonomousStore.CriadorApp.Models;

/// <param name="Status">"Ativa" ou "Suspensa".</param>
public record EmpresaDto(Guid Id, int Codigo, string Nome, string Slug, string Status, int LimiteDeLojas, DateTime CriadaEm);

public record CriarEmpresaRequest(
    string Nome, string Slug, int LimiteDeLojas, string AdminNome, string AdminEmail, string AdminSenha);

public record DefinirLimiteDeLojasRequest(int Limite);

public record LojaDaEmpresaDto(Guid Id, string Nome, string? Segmento, bool Ativa, DateTime CriadaEm);

public record LojasDaEmpresaDto(List<LojaDaEmpresaDto> Lojas, int Ativas, int Limite);

/// <param name="Segmento">O tipo de negócio ("Comida", "Roupa"). Texto livre, só descreve.</param>
public record SalvarLojaRequest(string Nome, string? Segmento);
