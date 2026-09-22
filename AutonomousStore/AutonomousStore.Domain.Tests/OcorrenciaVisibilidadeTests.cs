using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Tests;

/// <summary>
/// O que é do Admin e o que é do suporte. O Admin enxerga o que os detectores acharam e os pedidos que ELE escreveu; o pedido
/// de um comprador é do suporte. A pergunta "este pedido é de outra pessoa?" é o que decide isso.
/// </summary>
public class OcorrenciaVisibilidadeTests
{
    private static Ocorrencia Detectada() => new(
        DateTime.UtcNow, "WebApi", "Modulo", "Operacao", TipoDeOcorrencia.ErroExecucao, Severidade.Media,
        "algo deu errado", AcaoRecomendada.ApenasRegistrar);

    private static Ocorrencia PedidoDe(string app, string email)
        => Deteccoes.PedidoAoSuporte(app, ehMudanca: false, "Ajuda", "Preciso de ajuda", "Fulano", email, "/", DateTime.UtcNow);

    [Theory]
    [InlineData("ana@empresa.invalid")]
    [InlineData("outra@pessoa.invalid")]
    [InlineData(null)]
    [InlineData("")]
    public void OQueUmDetectorAchouNaoTemDonoEPortantoNuncaEDeOutraPessoa(string? quemPergunta)
    {
        Assert.False(Detectada().EhPedidoDeOutraPessoa(quemPergunta));
    }

    [Fact]
    public void OPedidoQueOProprioAdminEscreveEDele()
    {
        var pedido = PedidoDe("AdminApp", "ana@empresa.invalid");

        Assert.False(pedido.EhPedidoDeOutraPessoa("ana@empresa.invalid"));
    }

    [Theory]
    [InlineData("ANA@Empresa.INVALID")]
    [InlineData("  ana@empresa.invalid  ")]
    public void OEmailNaoDiferenciaMaiusculaDeMinusculaNemEspacoNasPontas(string quemPergunta)
    {
        Assert.False(PedidoDe("AdminApp", "ana@empresa.invalid").EhPedidoDeOutraPessoa(quemPergunta));
    }

    [Fact]
    public void OPedidoDeUmCompradorEDeOutraPessoaParaOAdmin()
    {
        var pedidoDoComprador = PedidoDe("ClientApp", "maria@compradora.invalid");

        Assert.True(pedidoDoComprador.EhPedidoDeOutraPessoa("ana@empresa.invalid"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void QuemNaoSabeQuemEEntraComoOutraPessoa(string? semEmail)
    {
        // Falha fechada: um Admin cujo token veio sem e-mail nao passa a enxergar os pedidos dos compradores.
        Assert.True(PedidoDe("ClientApp", "maria@compradora.invalid").EhPedidoDeOutraPessoa(semEmail));
    }
}
