using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Tests;

/// <summary>
/// O estoque é o que o sistema promete ao cliente. Se ele mentir, alguém compra
/// o que não existe — e a loja só descobre quando a pessoa já foi embora.
/// </summary>
public class ProductTests
{
    private static Product NovoProduto(decimal preco = 6.90m, int estoque = 10)
        => new("Energético Baly Cereja", "7891234567890", preco, stockQuantity: estoque);

    // --------------------------------------------------------------- criação
    [Fact]
    public void ProdutoNasceAtivoComOsDadosInformados()
    {
        var produto = NovoProduto();

        Assert.Equal("Energético Baly Cereja", produto.Name);
        Assert.Equal("7891234567890", produto.Barcode);
        Assert.Equal(6.90m, produto.Price);
        Assert.Equal(10, produto.StockQuantity);
        Assert.True(produto.IsActive);
        Assert.Null(produto.RfidTag);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NomeVazioERecusado(string? nome)
    {
        Assert.Throws<ArgumentException>(() => new Product(nome!, "789", 1.00m));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CodigoDeBarrasVazioERecusado(string? codigo)
    {
        Assert.Throws<ArgumentException>(() => new Product("Produto", codigo!, 1.00m));
    }

    [Fact]
    public void EstoqueInicialNegativoERecusado()
    {
        Assert.Throws<ArgumentException>(
            () => new Product("Produto", "789", 1.00m, stockQuantity: -1));
    }

    /// <summary>
    /// DEFEITO CORRIGIDO EM 23/08. `UpdatePrice` recusava preço negativo, mas o
    /// construtor não: dava para cadastrar um produto a -10,00 e cada leitura dele
    /// creditaria o cliente. A mesma regra precisa valer nos dois caminhos.
    /// </summary>
    [Fact]
    public void PrecoNegativoERecusadoNaCriacao()
    {
        Assert.Throws<ArgumentException>(() => new Product("Produto", "789", -10.00m));
    }

    [Fact]
    public void PrecoZeradoEAceito()
    {
        var produto = new Product("Brinde", "789", 0m);

        Assert.Equal(0m, produto.Price);
    }

    // ----------------------------------------------------------------- preço
    [Fact]
    public void AtualizarPrecoTrocaOValor()
    {
        var produto = NovoProduto();

        produto.UpdatePrice(8.50m);

        Assert.Equal(8.50m, produto.Price);
    }

    [Fact]
    public void AtualizarParaPrecoNegativoERecusado()
    {
        var produto = NovoProduto();

        Assert.Throws<ArgumentException>(() => produto.UpdatePrice(-1.00m));
        Assert.Equal(6.90m, produto.Price);
    }

    // --------------------------------------------------------------- estoque
    [Fact]
    public void ProdutoSaindoDaPrateleiraBaixaOEstoque()
    {
        var produto = NovoProduto(estoque: 10);

        produto.DecreaseStock();

        Assert.Equal(9, produto.StockQuantity);
    }

    [Fact]
    public void ProdutoVoltandoParaPrateleiraSobeOEstoque()
    {
        var produto = NovoProduto(estoque: 10);

        produto.IncreaseStock(3);

        Assert.Equal(13, produto.StockQuantity);
    }

    /// <summary>
    /// Estoque negativo não existe no mundo físico. Se a leitura disser que saiu
    /// mais do que havia, isso é divergência a investigar — e não motivo para
    /// travar a venda de quem está na loja agora.
    /// </summary>
    [Fact]
    public void EstoqueNuncaFicaNegativo()
    {
        var produto = NovoProduto(estoque: 2);

        produto.DecreaseStock(5);

        Assert.Equal(0, produto.StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void BaixarQuantidadeNaoPositivaERecusado(int quantidade)
    {
        var produto = NovoProduto(estoque: 10);

        Assert.Throws<ArgumentException>(() => produto.DecreaseStock(quantidade));
        Assert.Equal(10, produto.StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ReporQuantidadeNaoPositivaERecusado(int quantidade)
    {
        var produto = NovoProduto(estoque: 10);

        Assert.Throws<ArgumentException>(() => produto.IncreaseStock(quantidade));
        Assert.Equal(10, produto.StockQuantity);
    }

    // ---------------------------------------------------------- estoque baixo
    [Fact]
    public void SemLimiteDefinidoNuncaAvisaEstoqueBaixo()
    {
        var produto = NovoProduto(estoque: 0);

        Assert.False(produto.IsLowStock);
    }

    [Fact]
    public void AvisaEstoqueBaixoAoAtingirOLimite()
    {
        var produto = NovoProduto(estoque: 10);
        produto.SetMinimumStockThreshold(5);

        Assert.False(produto.IsLowStock);

        produto.DecreaseStock(5);

        Assert.True(produto.IsLowStock);
    }

    [Fact]
    public void LimiteMinimoNegativoERecusado()
    {
        var produto = NovoProduto();

        Assert.Throws<ArgumentException>(() => produto.SetMinimumStockThreshold(-1));
    }

    [Fact]
    public void LimiteMinimoPodeSerRemovido()
    {
        var produto = NovoProduto(estoque: 0);
        produto.SetMinimumStockThreshold(5);
        Assert.True(produto.IsLowStock);

        produto.SetMinimumStockThreshold(null);

        Assert.False(produto.IsLowStock);
    }

    // ------------------------------------------------------------- vínculos
    [Fact]
    public void VincularTagRfidGuardaAEtiqueta()
    {
        var produto = NovoProduto();

        produto.AssignRfidTag("04A2B3C4D5");

        Assert.Equal("04A2B3C4D5", produto.RfidTag);
    }

    [Fact]
    public void TagRfidPodeSerDesvinculada()
    {
        var produto = NovoProduto();
        produto.AssignRfidTag("04A2B3C4D5");

        produto.AssignRfidTag(null);

        Assert.Null(produto.RfidTag);
    }

    [Fact]
    public void DesativarESeguidoDeAtivarVoltaAoEstadoOriginal()
    {
        var produto = NovoProduto();

        produto.Deactivate();
        Assert.False(produto.IsActive);

        produto.Activate();
        Assert.True(produto.IsActive);
    }

    [Fact]
    public void AtualizarDetalhesComNomeVazioERecusado()
    {
        var produto = NovoProduto();

        Assert.Throws<ArgumentException>(() => produto.UpdateDetails("  ", null, null));
        Assert.Equal("Energético Baly Cereja", produto.Name);
    }
}
