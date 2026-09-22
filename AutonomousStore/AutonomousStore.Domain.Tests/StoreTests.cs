using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Tests;

/// <summary>
/// A loja física de uma empresa. O limite da assinatura conta as lojas ATIVAS, então "ativa" tem de ser o que a loja
/// diz ser: nasce ativa, e só deixa de ser por decisão.
/// </summary>
public class StoreTests
{
    [Fact]
    public void LojaNasceAtivaSemEmpresaAteSerGravada()
    {
        var loja = new Store("Loja Centro", "Comida");

        Assert.Equal("Loja Centro", loja.Nome);
        Assert.Equal("Comida", loja.Segmento);
        Assert.True(loja.IsActive);
        Assert.Null(loja.TenantId);         // quem carimba a empresa e o banco, ao gravar
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NomeVazioERecusado(string? nome)
    {
        Assert.Throws<ArgumentException>(() => new Store(nome!));
    }

    [Fact]
    public void NomeESegmentoSaemSemEspacoNasPontas()
    {
        var loja = new Store("  Loja Centro  ", "  Roupa ");

        Assert.Equal("Loja Centro", loja.Nome);
        Assert.Equal("Roupa", loja.Segmento);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SegmentoEmBrancoViraNulo(string? segmento)
    {
        Assert.Null(new Store("Loja", segmento).Segmento);
    }

    [Fact]
    public void AtualizarMudaNomeESegmento()
    {
        var loja = new Store("Loja Centro", "Comida");

        loja.AtualizarDados("Loja Norte", "Roupa");

        Assert.Equal("Loja Norte", loja.Nome);
        Assert.Equal("Roupa", loja.Segmento);
    }

    [Fact]
    public void AtualizarComNomeVazioERecusadoESemAlterarNada()
    {
        var loja = new Store("Loja Centro", "Comida");

        Assert.Throws<ArgumentException>(() => loja.AtualizarDados("  ", "Roupa"));

        Assert.Equal("Loja Centro", loja.Nome);
        Assert.Equal("Comida", loja.Segmento);
    }

    [Fact]
    public void DesativarELiberaAVagaEReativarADevolve()
    {
        var loja = new Store("Loja Centro");

        loja.Deactivate();
        Assert.False(loja.IsActive);

        loja.Activate();
        Assert.True(loja.IsActive);
    }

    // ═══════════════════════════════ o tamanho das colunas (Nome 200, Segmento 100)
    [Fact]
    public void NomeNoTamanhoMaximoEAceito()
    {
        var nome = new string('n', Store.TamanhoMaximoDoNome);
        var segmento = new string('s', Store.TamanhoMaximoDoSegmento);

        var loja = new Store(nome, segmento);

        Assert.Equal(nome, loja.Nome);
        Assert.Equal(segmento, loja.Segmento);
    }

    [Fact]
    public void NomeMaiorQueAColunaERecusadoComUmaFrase()
    {
        var erro = Assert.Throws<ArgumentException>(() => new Store(new string('n', Store.TamanhoMaximoDoNome + 1)));

        Assert.Equal("O nome da loja pode ter no máximo 200 caracteres.", erro.Message);
    }

    [Fact]
    public void SegmentoMaiorQueAColunaERecusado()
    {
        var erro = Assert.Throws<ArgumentException>(
            () => new Store("Loja", new string('s', Store.TamanhoMaximoDoSegmento + 1)));

        Assert.Equal("O segmento pode ter no máximo 100 caracteres.", erro.Message);
    }

    [Fact]
    public void OsEspacosDasPontasNaoContamNoTamanho()
    {
        // 200 letras e espacos em volta: depois de aparado cabe na coluna.
        var loja = new Store("  " + new string('n', Store.TamanhoMaximoDoNome) + "  ");

        Assert.Equal(Store.TamanhoMaximoDoNome, loja.Nome.Length);
    }

    [Fact]
    public void AtualizarComNomeGrandeDemaisERecusadoESemAlterarNada()
    {
        var loja = new Store("Loja Centro", "Comida");

        Assert.Throws<ArgumentException>(
            () => loja.AtualizarDados(new string('n', Store.TamanhoMaximoDoNome + 1), "Roupa"));

        Assert.Equal("Loja Centro", loja.Nome);
        Assert.Equal("Comida", loja.Segmento);         // o segmento novo tambem nao entrou: nada fica pela metade
    }

    [Fact]
    public void AMensagemDeErroNaoTrazONomeDoParametro()
    {
        // O controller devolve Message ao Admin; " (Parameter 'nome')" nao e frase para quem usa o painel.
        var vazio = Assert.Throws<ArgumentException>(() => new Store("  "));
        var grande = Assert.Throws<ArgumentException>(() => new Store(new string('n', 201)));

        Assert.DoesNotContain("Parameter", vazio.Message);
        Assert.DoesNotContain("Parameter", grande.Message);
    }

    [Fact]
    public void ADesativacaoNaoApagaNada()
    {
        var loja = new Store("Loja Centro", "Comida");

        loja.Deactivate();

        // Desativar e suspender, nao apagar: o historico da loja continua onde estava.
        Assert.Equal("Loja Centro", loja.Nome);
        Assert.Equal("Comida", loja.Segmento);
    }
}
