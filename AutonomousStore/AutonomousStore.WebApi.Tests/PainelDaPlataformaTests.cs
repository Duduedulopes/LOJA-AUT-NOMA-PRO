using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.WebApi.Services;

namespace AutonomousStore.WebApi.Tests;

/// <summary>
/// As contas do painel do Criador. O que a tela mostra ao dono da plataforma tem de bater com o que existe: um número errado
/// aqui vira decisão errada (suspender a empresa errada, achar que o suporte está em dia).
/// </summary>
public class PainelDaPlataformaTests
{
    private static readonly DateTime Agora = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);

    private static Tenant Empresa(string nome, int codigo, int limite = 2, bool suspensa = false)
    {
        var empresa = new Tenant(nome, nome.ToLowerInvariant().Replace(' ', '-'), limite);
        empresa.AtribuirCodigo(codigo);
        if (suspensa) empresa.Suspender();
        return empresa;
    }

    private static Store Loja(Tenant empresa, bool ativa = true)
    {
        var loja = new Store("Loja " + Guid.NewGuid().ToString("N")[..6]);
        loja.PertenceAoTenant(empresa.Id);
        if (!ativa) loja.Deactivate();
        return loja;
    }

    private static readonly IReadOnlyDictionary<Guid, int> SemCompradores = new Dictionary<Guid, int>();

    private static (Guid? TenantId, Severidade Severidade, DateTime QuandoUtc) NaFila(
        Guid? empresa, Severidade severidade, double horasAtras)
        => (empresa, severidade, Agora.AddHours(-horasAtras));

    private static (string? ResolvidaPor, DateTime QuandoUtc, DateTime ResolvidaEm) Resolvida(string? quem, double horasParaResolver)
        => (quem, Agora.AddDays(-2), Agora.AddDays(-2).AddHours(horasParaResolver));

    private static readonly IReadOnlyList<(Guid? TenantId, Severidade Severidade, DateTime QuandoUtc)> FilaVazia = [];
    private static readonly IReadOnlyList<(string? ResolvidaPor, DateTime QuandoUtc, DateTime ResolvidaEm)> NadaResolvido = [];

    // ═══════════════════════════════════════════════════ empresas e lojas
    [Fact]
    public void ContaAsEmpresasAtivasEAsSuspensas()
    {
        var empresas = new[] { Empresa("Alfa", 1), Empresa("Beta", 2), Empresa("Gama", 3, suspensa: true) };

        var painel = PainelDaPlataforma.Montar(empresas, [], SemCompradores, 0, FilaVazia, NadaResolvido, Agora);

        Assert.Equal(3, painel.Empresas.Total);
        Assert.Equal(2, painel.Empresas.Ativas);
        Assert.Equal(1, painel.Empresas.Suspensas);
    }

    [Fact]
    public void LojasELimiteSoContamAsEmpresasAtivas()
    {
        var alfa = Empresa("Alfa", 1, limite: 3);
        var gama = Empresa("Gama", 3, limite: 5, suspensa: true);
        var lojas = new[] { Loja(alfa), Loja(alfa), Loja(alfa, ativa: false), Loja(gama), Loja(gama) };

        var painel = PainelDaPlataforma.Montar([alfa, gama], lojas, SemCompradores, 0, FilaVazia, NadaResolvido, Agora);

        // A suspensa tem 2 lojas e limite 5, mas nao esta pagando: nao entra na conta. E a desativada da Alfa nao ocupa vaga.
        Assert.Equal(2, painel.Lojas.Ativas);
        Assert.Equal(3, painel.Lojas.Limite);
    }

    [Fact]
    public void CadaEmpresaTemAsPropriasContas_EAsLinhasVemEmOrdemDeCodigo()
    {
        var beta = Empresa("Beta", 2);
        var alfa = Empresa("Alfa", 1);
        var lojas = new[] { Loja(alfa), Loja(beta), Loja(beta) };
        var compradores = new Dictionary<Guid, int> { [alfa.Id] = 7, [beta.Id] = 3 };
        var fila = new[] { NaFila(beta.Id, Severidade.Media, 1), NaFila(beta.Id, Severidade.Alta, 2), NaFila(alfa.Id, Severidade.Baixa, 3) };

        var painel = PainelDaPlataforma.Montar([beta, alfa], lojas, compradores, 4, fila, NadaResolvido, Agora);

        Assert.Equal(new[] { "0001 · Alfa", "0002 · Beta" }, painel.PorEmpresa.Select(l => l.Rotulo).ToArray());
        Assert.Equal((1, 7, 1), (painel.PorEmpresa[0].LojasAtivas, painel.PorEmpresa[0].Compradores, painel.PorEmpresa[0].NaFila));
        Assert.Equal((2, 3, 2), (painel.PorEmpresa[1].LojasAtivas, painel.PorEmpresa[1].Compradores, painel.PorEmpresa[1].NaFila));
        Assert.Equal(10, painel.Compradores);
        Assert.Equal(4, painel.Tecnicos);
    }

    [Fact]
    public void EmpresaSemNadaApareceComZeros_NaoSome()
    {
        var alfa = Empresa("Alfa", 1);

        var painel = PainelDaPlataforma.Montar([alfa], [], SemCompradores, 0, FilaVazia, NadaResolvido, Agora);

        var linha = Assert.Single(painel.PorEmpresa);
        Assert.Equal((0, 0, 0), (linha.LojasAtivas, linha.Compradores, linha.NaFila));
    }

    // ══════════════════════════════════════════════════════════ a fila
    [Fact]
    public void AFilaContaGravesEMedeOMaisAntigo()
    {
        var alfa = Empresa("Alfa", 1);
        var fila = new[]
        {
            NaFila(alfa.Id, Severidade.Baixa, 1),
            NaFila(alfa.Id, Severidade.Alta, 5.5),
            NaFila(alfa.Id, Severidade.Critica, 30),
            NaFila(alfa.Id, Severidade.Media, 2),
        };

        var suporte = PainelDaPlataforma.ResumirSuporte(fila, NadaResolvido, Agora);

        Assert.Equal(4, suporte.NaFila);
        Assert.Equal(2, suporte.Graves);                 // Alta e Critica; a Media nao
        Assert.Equal(30.0, suporte.HorasDoMaisAntigo);
    }

    [Fact]
    public void ChamadoDaPlataformaContaNoTotalMasEmNenhumaEmpresa()
    {
        var alfa = Empresa("Alfa", 1);
        var fila = new[] { NaFila(null, Severidade.Alta, 1), NaFila(alfa.Id, Severidade.Baixa, 1) };

        var painel = PainelDaPlataforma.Montar([alfa], [], SemCompradores, 0, fila, NadaResolvido, Agora);

        Assert.Equal(2, painel.Suporte.NaFila);
        Assert.Equal(1, painel.PorEmpresa[0].NaFila);
    }

    [Fact]
    public void FilaVaziaNaoInventaNumero()
    {
        var suporte = PainelDaPlataforma.ResumirSuporte(FilaVazia, NadaResolvido, Agora);

        Assert.Equal(0, suporte.NaFila);
        Assert.Equal(0, suporte.Graves);
        Assert.Null(suporte.HorasDoMaisAntigo);          // "0 horas" diria que ha um chamado novinho; nulo diz que nao ha nenhum
        Assert.Null(suporte.HorasMediasParaResolver);
        Assert.Empty(suporte.PorQuemResolveu);
    }

    // ═════════════════════════════════════════════════ o que foi resolvido
    [Fact]
    public void AMediaDeResolucaoEDeTodas_EQuemResolveuVemDoMaisAoMenos()
    {
        var resolvidas = new[]
        {
            Resolvida("Bia", 2), Resolvida("Bia", 4), Resolvida("Bia", 6),
            Resolvida("Carlos", 10),
        };

        var suporte = PainelDaPlataforma.ResumirSuporte(FilaVazia, resolvidas, Agora);

        Assert.Equal(4, suporte.Resolvidas30Dias);
        Assert.Equal(5.5, suporte.HorasMediasParaResolver);          // (2+4+6+10)/4
        Assert.Equal(new[] { "Bia", "Carlos" }, suporte.PorQuemResolveu.Select(p => p.Nome).ToArray());
        Assert.Equal((3, 4.0), (suporte.PorQuemResolveu[0].Quantidade, suporte.PorQuemResolveu[0].HorasMedias));
        Assert.Equal((1, 10.0), (suporte.PorQuemResolveu[1].Quantidade, suporte.PorQuemResolveu[1].HorasMedias));
    }

    [Fact]
    public void QuemNaoTemNomeViraUmaLinhaPropria_ENaoSomeDaContagem()
    {
        var resolvidas = new[] { Resolvida(null, 1), Resolvida("  ", 1), Resolvida("Bia", 1) };

        var suporte = PainelDaPlataforma.ResumirSuporte(FilaVazia, resolvidas, Agora);

        Assert.Equal(3, suporte.Resolvidas30Dias);
        Assert.Contains(suporte.PorQuemResolveu, p => p.Nome == "(não informado)" && p.Quantidade == 2);
    }

    [Fact]
    public void NomesComEspacoNasPontasOuCaixaDiferenteContamPorPessoa_ENaoSaiMaisQueCinco()
    {
        var resolvidas = new[]
        {
            Resolvida("Bia", 1), Resolvida(" Bia ", 1),
            Resolvida("A", 1), Resolvida("B", 1), Resolvida("C", 1), Resolvida("D", 1), Resolvida("E", 1),
        };

        var suporte = PainelDaPlataforma.ResumirSuporte(FilaVazia, resolvidas, Agora);

        Assert.Equal(5, suporte.PorQuemResolveu.Count);
        Assert.Equal("Bia", suporte.PorQuemResolveu[0].Nome);
        Assert.Equal(2, suporte.PorQuemResolveu[0].Quantidade);     // "Bia" e " Bia " sao a mesma pessoa
    }

    [Fact]
    public void RelogioDesalinhadoNaoViraTempoNegativo()
    {
        var resolvidas = new[] { Resolvida("Bia", -3) };           // "resolvida" antes de nascer: dado torto, nunca -3 h na tela

        var suporte = PainelDaPlataforma.ResumirSuporte(FilaVazia, resolvidas, Agora);

        Assert.Equal(0.0, suporte.HorasMediasParaResolver);
        Assert.Equal(0.0, suporte.PorQuemResolveu[0].HorasMedias);
    }
}
