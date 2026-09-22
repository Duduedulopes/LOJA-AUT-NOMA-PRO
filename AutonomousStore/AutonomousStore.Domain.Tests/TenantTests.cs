using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Tests;

/// <summary>
/// A empresa é a fronteira entre um cliente e outro. O identificador dela vai no
/// link da loja, o limite de lojas é o que o preço acompanha, e a suspensão é o
/// botão de quem parou de pagar — todos três precisam se comportar sem surpresa.
/// </summary>
public class TenantTests
{
    private static Tenant NovaEmpresa(string slug = "redesabor", int limite = 2)
        => new("Rede Sabor", slug, limite);

    // --------------------------------------------------------------- criação
    [Fact]
    public void EmpresaNasceAtivaComOsDadosInformados()
    {
        var empresa = NovaEmpresa();

        Assert.Equal("Rede Sabor", empresa.Nome);
        Assert.Equal("redesabor", empresa.Slug);
        Assert.Equal(2, empresa.LimiteDeLojas);
        Assert.Equal(StatusDoTenant.Ativa, empresa.Status);
        Assert.True(empresa.EstaAtiva);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NomeVazioERecusado(string? nome)
    {
        Assert.Throws<ArgumentException>(() => new Tenant(nome!, "redesabor"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void EmpresaSemNenhumaLojaPermitidaERecusada(int limite)
    {
        Assert.Throws<ArgumentException>(() => new Tenant("Rede Sabor", "redesabor", limite));
    }

    // ------------------------------------------------------------------ slug
    [Fact]
    public void SlugEGuardadoEmMinusculoESemEspacoNasPontas()
    {
        Assert.Equal("rede-sabor", new Tenant("Rede Sabor", "  Rede-Sabor ").Slug);
    }

    [Theory]
    [InlineData("ab")]                 // curto demais
    [InlineData("com espaco")]
    [InlineData("com_underline")]
    [InlineData("acentuação")]
    [InlineData("-comeca-com-hifen")]
    [InlineData("termina-com-hifen-")]
    [InlineData("dois--hifens")]
    [InlineData("")]
    [InlineData(null)]
    public void SlugForaDoFormatoERecusado(string? slug)
    {
        Assert.Throws<ArgumentException>(() => new Tenant("Rede Sabor", slug!));
    }

    [Fact]
    public void SlugComMaisDeQuarentaCaracteresERecusado()
    {
        Assert.Throws<ArgumentException>(() => new Tenant("Rede Sabor", new string('a', 41)));
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("API")]                // reservado, mesmo em maiúsculo
    [InlineData("criador")]
    [InlineData("suporte")]
    public void SlugReservadoPeloSistemaERecusado(string slug)
    {
        Assert.Throws<ArgumentException>(() => new Tenant("Rede Sabor", slug));
    }

    // ---------------------------------------------------------------- limite
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]             // já está no limite de 2
    [InlineData(5, false)]
    public void SoPodeTerMaisUmaLojaEnquantoNaoChegouNoLimite(int lojasAtuais, bool esperado)
    {
        Assert.Equal(esperado, NovaEmpresa(limite: 2).PodeTerMaisUmaLoja(lojasAtuais));
    }

    [Fact]
    public void AumentarOLimiteLiberaMaisLojas()
    {
        var empresa = NovaEmpresa(limite: 1);
        Assert.False(empresa.PodeTerMaisUmaLoja(1));

        empresa.DefinirLimiteDeLojas(3);

        Assert.True(empresa.PodeTerMaisUmaLoja(1));
    }

    [Fact]
    public void LimiteZeroNaoPodeSerDefinidoDepois()
    {
        Assert.Throws<ArgumentException>(() => NovaEmpresa().DefinirLimiteDeLojas(0));
    }

    // ---------------------------------------------------------------- código
    [Fact]
    public void EmpresaNovaAindaNaoTemCodigoAteSerCadastrada()
    {
        Assert.Equal(0, NovaEmpresa().Codigo);
    }

    [Fact]
    public void RotuloParaOSuporteEOCodigoComQuatroDigitosMaisONome()
    {
        var empresa = NovaEmpresa();
        empresa.AtribuirCodigo(42);

        Assert.Equal("0042 · Rede Sabor", empresa.Rotulo);
    }

    [Fact]
    public void RotuloNaoDeixaVazarNadaAlemDeCodigoENome()
    {
        var empresa = NovaEmpresa();
        empresa.AtribuirCodigo(7);

        // O tecnico enxerga so isto da empresa: nem o slug (o endereco publico)
        // nem o limite de lojas (o plano contratado) fazem parte do rotulo.
        Assert.DoesNotContain(empresa.Slug, empresa.Rotulo);
        Assert.Equal("0007 · Rede Sabor", empresa.Rotulo);
    }

    [Fact]
    public void CodigoSoPodeSerDadoUmaVez()
    {
        var empresa = NovaEmpresa();
        empresa.AtribuirCodigo(1);

        Assert.Throws<InvalidOperationException>(() => empresa.AtribuirCodigo(2));
        Assert.Equal(1, empresa.Codigo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void CodigoComecaEmUm(int codigo)
    {
        Assert.Throws<ArgumentException>(() => NovaEmpresa().AtribuirCodigo(codigo));
    }

    // ------------------------------------------------------------ suspensão
    [Fact]
    public void SuspenderCortaAEmpresaEReativarDevolve()
    {
        var empresa = NovaEmpresa();

        empresa.Suspender();
        Assert.Equal(StatusDoTenant.Suspensa, empresa.Status);
        Assert.False(empresa.EstaAtiva);

        empresa.Reativar();
        Assert.True(empresa.EstaAtiva);
    }
}
