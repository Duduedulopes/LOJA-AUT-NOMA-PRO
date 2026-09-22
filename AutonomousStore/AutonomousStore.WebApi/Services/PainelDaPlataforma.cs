using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.WebApi.Contracts.Platform;

namespace AutonomousStore.WebApi.Services;

/// <summary>
/// Os números do painel do Criador. PURO: recebe o que o controller leu do banco e devolve o que a tela mostra. Ficar fora do
/// controller é o que permite testar cada conta sem subir servidor nem banco.
/// </summary>
public static class PainelDaPlataforma
{
    public const int DiasDoDesempenho = 30;

    private const int QuantosNaListaDeQuemResolveu = 5;
    private const string SemNome = "(não informado)";

    public static PainelResponse Montar(
        IReadOnlyList<Tenant> empresas,
        IReadOnlyList<Store> lojas,
        IReadOnlyDictionary<Guid, int> compradoresPorEmpresa,
        int tecnicosAtivos,
        IReadOnlyList<(Guid? TenantId, Severidade Severidade, DateTime QuandoUtc)> naFila,
        IReadOnlyList<(string? ResolvidaPor, DateTime QuandoUtc, DateTime ResolvidaEm)> resolvidas,
        DateTime agoraUtc)
    {
        var lojasAtivasPorEmpresa = lojas
            .Where(l => l.IsActive && l.TenantId is not null)
            .GroupBy(l => l.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var naFilaPorEmpresa = naFila
            .Where(o => o.TenantId is not null)
            .GroupBy(o => o.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var linhas = empresas
            .OrderBy(e => e.Codigo)
            .Select(e => new EmpresaNoPainel(
                e.Id, e.Codigo, e.Rotulo, e.Slug, e.Status, e.LimiteDeLojas,
                lojasAtivasPorEmpresa.GetValueOrDefault(e.Id),
                compradoresPorEmpresa.GetValueOrDefault(e.Id),
                naFilaPorEmpresa.GetValueOrDefault(e.Id)))
            .ToList();

        // Lojas e limite so olham as empresas ATIVAS: uma suspensa nao esta pagando, e somar o limite dela ao das outras
        // mostraria uma capacidade que ninguem contratou hoje.
        var ativas = linhas.Where(l => l.Status == StatusDoTenant.Ativa).ToList();

        return new PainelResponse(
            new ContagemDeEmpresas(linhas.Count, ativas.Count, linhas.Count - ativas.Count),
            new ContagemDeLojas(ativas.Sum(l => l.LojasAtivas), ativas.Sum(l => l.LimiteDeLojas)),
            tecnicosAtivos,
            compradoresPorEmpresa.Values.Sum(),
            ResumirSuporte(naFila, resolvidas, agoraUtc),
            linhas);
    }

    public static ResumoDoSuporte ResumirSuporte(
        IReadOnlyList<(Guid? TenantId, Severidade Severidade, DateTime QuandoUtc)> naFila,
        IReadOnlyList<(string? ResolvidaPor, DateTime QuandoUtc, DateTime ResolvidaEm)> resolvidas,
        DateTime agoraUtc)
    {
        var porQuem = resolvidas
            .GroupBy(r => string.IsNullOrWhiteSpace(r.ResolvidaPor) ? SemNome : r.ResolvidaPor!.Trim())
            .Select(g => new ResolvidasPor(g.Key, g.Count(), Arredondar(g.Average(r => Horas(r.ResolvidaEm - r.QuandoUtc)))))
            .OrderByDescending(x => x.Quantidade)
            .ThenBy(x => x.Nome, StringComparer.OrdinalIgnoreCase)
            .Take(QuantosNaListaDeQuemResolveu)
            .ToList();

        return new ResumoDoSuporte(
            NaFila: naFila.Count,
            Graves: naFila.Count(o => o.Severidade >= Severidade.Alta),
            HorasDoMaisAntigo: naFila.Count == 0 ? null : Arredondar(Horas(agoraUtc - naFila.Min(o => o.QuandoUtc))),
            Resolvidas30Dias: resolvidas.Count,
            HorasMediasParaResolver: resolvidas.Count == 0
                ? null
                : Arredondar(resolvidas.Average(r => Horas(r.ResolvidaEm - r.QuandoUtc))),
            PorQuemResolveu: porQuem);
    }

    // Um relogio desalinhado entre duas maquinas nao pode virar "resolvido em -3 horas" na tela do dono.
    private static double Horas(TimeSpan intervalo) => Math.Max(0, intervalo.TotalHours);

    private static double Arredondar(double horas) => Math.Round(horas, 1);
}
