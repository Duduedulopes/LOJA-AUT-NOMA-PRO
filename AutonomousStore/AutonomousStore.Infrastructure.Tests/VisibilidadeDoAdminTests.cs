using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Repositories;

namespace AutonomousStore.Infrastructure.Tests;

/// <summary>
/// O painel do Admin: o que os detectores acharam e os pedidos que ELE escreveu. Os pedidos dos compradores são do suporte, e
/// o Admin não os vê — nem na lista, nem no resumo, nem no sino (um número que promete o que a lista, logo depois, diz não
/// existir é pior que não avisar nada).
/// </summary>
public class VisibilidadeDoAdminTests : IDisposable
{
    private const string EmailDoAdmin = "ana@a.invalid";

    private readonly BancoDeTeste _banco = new();

    public void Dispose() => _banco.Dispose();

    private static Ocorrencia Detectada(string chave) => new(
        DateTime.UtcNow, "WebApi", "Modulo", "Operacao", TipoDeOcorrencia.ErroExecucao, Severidade.Media,
        "algo deu errado", AcaoRecomendada.ApenasRegistrar, chave: chave);

    private static Ocorrencia Pedido(string app, string email, bool mudanca = false)
        => Deteccoes.PedidoAoSuporte(app, mudanca, "Ajuda", "Preciso de ajuda", "Fulano", email, "/", DateTime.UtcNow);

    /// <summary>Na empresa A: 1 achada por detector, 1 pedido do Admin, 1 pedido de comprador (de mudança).</summary>
    private void PopularEmpresaA()
    {
        using var contexto = _banco.ComoEmpresa(_banco.EmpresaA);
        contexto.Ocorrencias.Add(Detectada("erro-x"));
        contexto.Ocorrencias.Add(Pedido("AdminApp", EmailDoAdmin));
        contexto.Ocorrencias.Add(Pedido("ClientApp", "maria@a.invalid", mudanca: true));
        contexto.SaveChanges();
    }

    private static async Task<List<string>> Descricoes(OcorrenciaRepository repo, FiltroDeOcorrencia filtro)
        => (await repo.BuscarAsync(filtro)).Select(o => o.Tipo.ToString()).OrderBy(t => t).ToList();

    [Fact]
    public async Task OAdminVeOQueODetectorAchouEOPedidoQueEleEscreveuMasNaoOPedidoDoComprador()
    {
        PopularEmpresaA();
        using var admin = _banco.ComoEmpresa(_banco.EmpresaA);

        var tipos = await Descricoes(new OcorrenciaRepository(admin), new FiltroDeOcorrencia(EmailDoAdmin: EmailDoAdmin));

        // ErroExecucao (o detector) e Duvida (o pedido dele). O PedidoDeMudanca do comprador nao esta.
        Assert.Equal(new[] { "Duvida", "ErroExecucao" }, tipos);
    }

    [Fact]
    public async Task OSuporteVeOsTresSemFiltro()
    {
        PopularEmpresaA();
        using var suporte = _banco.ComoPlataforma();

        var tipos = await Descricoes(new OcorrenciaRepository(suporte), new FiltroDeOcorrencia());

        Assert.Equal(new[] { "Duvida", "ErroExecucao", "PedidoDeMudanca" }, tipos);
    }

    [Theory]
    [InlineData("ANA@A.INVALID")]
    [InlineData("  ana@a.invalid  ")]
    public async Task OEmailDoAdminNaoDiferenciaMaiusculaNemEspaco(string email)
    {
        PopularEmpresaA();
        using var admin = _banco.ComoEmpresa(_banco.EmpresaA);

        var tipos = await Descricoes(new OcorrenciaRepository(admin), new FiltroDeOcorrencia(EmailDoAdmin: email));

        Assert.Contains("Duvida", tipos);                       // o pedido dele continua sendo dele
        Assert.DoesNotContain("PedidoDeMudanca", tipos);
    }

    [Fact]
    public async Task AdminSemEmailSoEnxergaOQueODetectorAchou()
    {
        PopularEmpresaA();
        using var admin = _banco.ComoEmpresa(_banco.EmpresaA);

        // Vazio LIGA o filtro (nao o desliga): falha fechada. Um Admin cujo token veio sem e-mail nao pode passar a
        // enxergar os pedidos dos compradores por causa de um descuido.
        var tipos = await Descricoes(new OcorrenciaRepository(admin), new FiltroDeOcorrencia(EmailDoAdmin: ""));

        Assert.Equal(new[] { "ErroExecucao" }, tipos);
    }

    [Fact]
    public async Task OResumoDoAdminContaSoOQueEleEnxerga()
    {
        PopularEmpresaA();
        var desde = DateTime.UtcNow.AddDays(-1);
        var ate = DateTime.UtcNow.AddDays(1);

        using var admin = _banco.ComoEmpresa(_banco.EmpresaA);
        using var suporte = _banco.ComoPlataforma();

        var doAdmin = await new OcorrenciaRepository(admin).ResumoAsync(desde, ate, EmailDoAdmin);
        var doSuporte = await new OcorrenciaRepository(suporte).ResumoAsync(desde, ate);

        Assert.Equal(2, doAdmin.Sum(l => l.Quantidade));
        Assert.DoesNotContain(doAdmin, l => l.Tipo == TipoDeOcorrencia.PedidoDeMudanca);

        Assert.Equal(3, doSuporte.Sum(l => l.Quantidade));
        Assert.Contains(doSuporte, l => l.Tipo == TipoDeOcorrencia.PedidoDeMudanca);
    }

    /// <summary>
    /// Um pedido "de outra pessoa" que por algum motivo ainda esteja em <see cref="EstadoDaOcorrencia.Nova"/>.
    /// </summary>
    /// <remarks>
    /// HOJE NENHUM CAMINHO REAL CRIA ISSO: todo pedido (<c>Deteccoes.PedidoAoSuporte</c>, e o "chamar o suporte" do Admin em
    /// <c>OcorrenciasController.Suporte</c>) marca o dono E manda para o suporte na MESMA chamada — o estado nunca fica
    /// "Nova com dono" de verdade. O teste continua valendo como uma trava: se algum caminho novo algum dia deixar essa
    /// combinação escapar, o sino não pode contar o pedido de outra pessoa por engano.
    /// </remarks>
    private static Ocorrencia PedidoAindaNaoDespachado(string email)
    {
        var o = Detectada("pedido-" + email);
        o.AbertoPorAlguem(email);
        return o;
    }

    [Fact]
    public async Task OSinoDoAdminNaoContariaUmPedidoDeOutraPessoaSeEleFicasseEmNova()
    {
        using (var contexto = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            contexto.Ocorrencias.Add(Detectada("erro-x"));                       // sem dono: conta para todos
            contexto.Ocorrencias.Add(PedidoAindaNaoDespachado(EmailDoAdmin));    // dono e o proprio Admin: conta
            contexto.Ocorrencias.Add(PedidoAindaNaoDespachado("maria@a.invalid")); // dono e outra pessoa: NAO conta pro Admin
            contexto.SaveChanges();
        }

        using var admin = _banco.ComoEmpresa(_banco.EmpresaA);
        using var suporte = _banco.ComoPlataforma();

        var doAdmin = await new OcorrenciaRepository(admin).NaoVistasAsync(EmailDoAdmin);
        var doSuporte = await new OcorrenciaRepository(suporte).NaoVistasAsync();

        Assert.Equal(2, doAdmin.Total);
        Assert.Equal(3, doSuporte.Total);
    }

    [Fact]
    public async Task OSinoSemEmailSoContaOQueODetectorAchou()
    {
        using (var contexto = _banco.ComoEmpresa(_banco.EmpresaA))
        {
            contexto.Ocorrencias.Add(Detectada("erro-x"));
            contexto.Ocorrencias.Add(PedidoAindaNaoDespachado(EmailDoAdmin));
            contexto.SaveChanges();
        }

        using var admin = _banco.ComoEmpresa(_banco.EmpresaA);

        // Vazio LIGA o filtro (nao o desliga): mesma regra da lista — falha fechada.
        var (total, _, _) = await new OcorrenciaRepository(admin).NaoVistasAsync("");

        Assert.Equal(1, total);
    }

    [Fact]
    public async Task OFiltroDoAdminNaoAbreAPortaParaOutraEmpresa()
    {
        PopularEmpresaA();
        using (var b = _banco.ComoEmpresa(_banco.EmpresaB))
        {
            b.Ocorrencias.Add(Detectada("erro-da-b"));      // sem dono, mas da empresa B
            b.SaveChanges();
        }

        using var adminDaA = _banco.ComoEmpresa(_banco.EmpresaA);
        var achadas = await new OcorrenciaRepository(adminDaA).BuscarAsync(new FiltroDeOcorrencia(EmailDoAdmin: EmailDoAdmin));

        Assert.DoesNotContain(achadas, o => o.Chave == "erro-da-b");
    }
}
