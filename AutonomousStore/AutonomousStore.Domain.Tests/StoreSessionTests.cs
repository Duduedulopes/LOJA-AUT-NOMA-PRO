using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Tests;

/// <summary>
/// A sessão é onde o dinheiro do cliente é contado. Cada teste aqui corresponde
/// a uma regra que, se quebrar, cobra o valor errado de alguém.
/// </summary>
public class StoreSessionTests
{
    private static readonly DateTime Agora = new(2026, 8, 23, 12, 0, 0, DateTimeKind.Utc);

    private static StoreSession NovaSessao() => new(Guid.NewGuid(), Agora);

    private static StoreSession SessaoAberta()
    {
        var sessao = NovaSessao();
        sessao.ConfirmEntry(sessao.QrCodeToken, Agora);
        return sessao;
    }

    // ------------------------------------------------------------ nascimento
    [Fact]
    public void SessaoNasceAguardandoEntradaComQrCodeValido()
    {
        var sessao = NovaSessao();

        Assert.Equal(SessionStatus.AguardandoEntrada, sessao.Status);
        Assert.False(string.IsNullOrWhiteSpace(sessao.QrCodeToken));
        Assert.Equal(Agora.AddMinutes(StoreSession.QrCodeValidityMinutes), sessao.QrCodeExpiresAt);
        Assert.Empty(sessao.Items);
        Assert.Equal(0m, sessao.Total);
    }

    [Fact]
    public void CadaSessaoRecebeUmTokenDiferente()
    {
        var primeira = NovaSessao();
        var segunda = NovaSessao();

        Assert.NotEqual(primeira.QrCodeToken, segunda.QrCodeToken);
    }

    // ----------------------------------------------------------------- entrada
    [Fact]
    public void ConfirmarEntradaComOTokenCertoAbreASessao()
    {
        var sessao = NovaSessao();

        sessao.ConfirmEntry(sessao.QrCodeToken, Agora);

        Assert.Equal(SessionStatus.Aberta, sessao.Status);
        Assert.Equal(Agora, sessao.EntryConfirmedAt);
    }

    [Fact]
    public void TokenErradoNaoAbreAPorta()
    {
        var sessao = NovaSessao();

        Assert.Throws<InvalidOperationException>(
            () => sessao.ConfirmEntry("token-de-outra-pessoa", Agora));

        Assert.Equal(SessionStatus.AguardandoEntrada, sessao.Status);
    }

    [Fact]
    public void QrCodeExpiradoNaoAbreAPorta()
    {
        var sessao = NovaSessao();
        var tarde = Agora.AddMinutes(StoreSession.QrCodeValidityMinutes + 1);

        Assert.Throws<InvalidOperationException>(
            () => sessao.ConfirmEntry(sessao.QrCodeToken, tarde));
    }

    [Fact]
    public void NaoDaParaEntrarDuasVezesComOMesmoQrCode()
    {
        var sessao = SessaoAberta();

        Assert.Throws<InvalidOperationException>(
            () => sessao.ConfirmEntry(sessao.QrCodeToken, Agora));
    }

    [Fact]
    public void GerarNovoQrCodeTrocaOTokenEEstendeAValidade()
    {
        var sessao = NovaSessao();
        var tokenAntigo = sessao.QrCodeToken;
        var depois = Agora.AddMinutes(10);

        sessao.RegenerateQrCode(depois);

        Assert.NotEqual(tokenAntigo, sessao.QrCodeToken);
        Assert.Equal(depois.AddMinutes(StoreSession.QrCodeValidityMinutes), sessao.QrCodeExpiresAt);
    }

    [Fact]
    public void NaoDaParaGerarQrCodeNovoComASessaoJaAberta()
    {
        var sessao = SessaoAberta();

        Assert.Throws<InvalidOperationException>(() => sessao.RegenerateQrCode(Agora));
    }

    // ------------------------------------------------------------------ itens
    [Fact]
    public void AdicionarItemSomaNoTotal()
    {
        var sessao = SessaoAberta();

        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);

        Assert.Single(sessao.Items);
        Assert.Equal(3.50m, sessao.Total);
    }

    [Fact]
    public void OMesmoProdutoDuasVezesViraUmItemComQuantidadeDois()
    {
        var sessao = SessaoAberta();
        var produto = Guid.NewGuid();

        sessao.AddItem(produto, "Coca-Cola Lata 350ml", 5.50m);
        sessao.AddItem(produto, "Coca-Cola Lata 350ml", 5.50m);

        Assert.Single(sessao.Items);
        Assert.Equal(2, sessao.Items[0].Quantity);
        Assert.Equal(11.00m, sessao.Total);
    }

    [Fact]
    public void ProdutosDiferentesViramItensDiferentes()
    {
        var sessao = SessaoAberta();

        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);
        sessao.AddItem(Guid.NewGuid(), "Energético Baly", 6.90m);

        Assert.Equal(2, sessao.Items.Count);
        Assert.Equal(10.40m, sessao.Total);
    }

    /// <summary>
    /// DEFEITO CORRIGIDO EM 23/08. `IncreaseQuantity` não validava a entrada, então
    /// `AddItem` com quantidade negativa num produto que já estava no carrinho
    /// DIMINUÍA a quantidade. O construtor de SessionItem validava; o caminho do
    /// item existente, não. Adicionar reduzia a conta.
    /// </summary>
    [Fact]
    public void AdicionarQuantidadeNegativaEmItemExistenteERecusado()
    {
        var sessao = SessaoAberta();
        var produto = Guid.NewGuid();
        sessao.AddItem(produto, "Energético Baly", 6.90m, 3);

        Assert.Throws<ArgumentException>(() => sessao.AddItem(produto, "Energético Baly", 6.90m, -2));

        Assert.Equal(3, sessao.Items[0].Quantity);
        Assert.Equal(20.70m, sessao.Total);
    }

    [Fact]
    public void AdicionarQuantidadeZeradaERecusado()
    {
        var sessao = SessaoAberta();

        Assert.Throws<ArgumentException>(
            () => sessao.AddItem(Guid.NewGuid(), "Energético Baly", 6.90m, 0));
    }

    [Fact]
    public void PrecoNegativoNaoEntraNoCarrinho()
    {
        var sessao = SessaoAberta();

        Assert.Throws<ArgumentException>(
            () => sessao.AddItem(Guid.NewGuid(), "Produto Suspeito", -10.00m));
    }

    [Fact]
    public void RemoverItemAbaixaOTotal()
    {
        var sessao = SessaoAberta();
        var produto = Guid.NewGuid();
        sessao.AddItem(produto, "Coca-Cola Lata 350ml", 5.50m, 2);

        sessao.RemoveItem(produto);

        Assert.Equal(1, sessao.Items[0].Quantity);
        Assert.Equal(5.50m, sessao.Total);
    }

    [Fact]
    public void RemoverAUltimaUnidadeTiraOItemDoCarrinho()
    {
        var sessao = SessaoAberta();
        var produto = Guid.NewGuid();
        sessao.AddItem(produto, "Coca-Cola Lata 350ml", 5.50m);

        sessao.RemoveItem(produto);

        Assert.Empty(sessao.Items);
        Assert.Equal(0m, sessao.Total);
    }

    /// <summary>
    /// DEFEITO CORRIGIDO EM 23/08. `DecreaseQuantity` fazia `Math.Max(0, Quantity - quantity)`.
    /// Com quantidade negativa isso vira uma SOMA: remover 5 unidades de um carrinho
    /// com 2 deixava 7. Um cliente que soubesse disso encheria o carrinho pedindo
    /// remoções.
    /// </summary>
    [Fact]
    public void RemoverQuantidadeNegativaERecusado()
    {
        var sessao = SessaoAberta();
        var produto = Guid.NewGuid();
        sessao.AddItem(produto, "Energético Baly", 6.90m, 2);

        Assert.Throws<ArgumentException>(() => sessao.RemoveItem(produto, -5));

        Assert.Equal(2, sessao.Items[0].Quantity);
    }

    [Fact]
    public void RemoverProdutoQueNaoEstaNoCarrinhoNaoEstoura()
    {
        var sessao = SessaoAberta();
        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);

        sessao.RemoveItem(Guid.NewGuid());

        Assert.Single(sessao.Items);
    }

    [Fact]
    public void NaoDaParaAdicionarItemAntesDeConfirmarAEntrada()
    {
        var sessao = NovaSessao();

        Assert.Throws<InvalidOperationException>(
            () => sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m));
    }

    [Fact]
    public void OPrecoDoItemFicaCongeladoNoMomentoDaLeitura()
    {
        var sessao = SessaoAberta();
        var produto = Guid.NewGuid();

        sessao.AddItem(produto, "Energético Baly", 6.90m);
        sessao.AddItem(produto, "Energético Baly", 9.90m);

        // A segunda leitura só soma quantidade: quem manda é o preço da primeira.
        Assert.Equal(13.80m, sessao.Total);
    }

    // --------------------------------------------------------------- checkout
    [Fact]
    public void FecharACompraTravaASessaoEmAguardandoPagamento()
    {
        var sessao = SessaoAberta();
        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);

        sessao.RequestCheckout(Agora);

        Assert.Equal(SessionStatus.AguardandoPagamento, sessao.Status);
        Assert.Equal(Agora, sessao.ClosedAt);
    }

    [Fact]
    public void CarrinhoVazioNaoFechaCompra()
    {
        var sessao = SessaoAberta();

        Assert.Throws<InvalidOperationException>(() => sessao.RequestCheckout(Agora));
        Assert.Equal(SessionStatus.Aberta, sessao.Status);
    }

    [Fact]
    public void DepoisDoCheckoutOCarrinhoNaoMudaMais()
    {
        var sessao = SessaoAberta();
        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);
        sessao.RequestCheckout(Agora);

        Assert.Throws<InvalidOperationException>(
            () => sessao.AddItem(Guid.NewGuid(), "Energético Baly", 6.90m));
        Assert.Throws<InvalidOperationException>(
            () => sessao.RemoveItem(sessao.Items[0].ProductId));

        Assert.Equal(3.50m, sessao.Total);
    }

    // -------------------------------------------------------------- pagamento
    [Fact]
    public void ConfirmarPagamentoConcluiASessao()
    {
        var sessao = SessaoAberta();
        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);
        sessao.RequestCheckout(Agora);
        var meioDePagamento = Guid.NewGuid();

        sessao.ConfirmPayment(meioDePagamento, Agora);

        Assert.Equal(SessionStatus.Concluida, sessao.Status);
        Assert.Equal(meioDePagamento, sessao.PaymentMethodId);
        Assert.Equal(Agora, sessao.PaymentConfirmedAt);
    }

    [Fact]
    public void NaoDaParaPagarSemFecharACompra()
    {
        var sessao = SessaoAberta();
        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);

        Assert.Throws<InvalidOperationException>(
            () => sessao.ConfirmPayment(Guid.NewGuid(), Agora));
    }

    [Fact]
    public void NaoDaParaPagarDuasVezes()
    {
        var sessao = SessaoAberta();
        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);
        sessao.RequestCheckout(Agora);
        sessao.ConfirmPayment(Guid.NewGuid(), Agora);

        Assert.Throws<InvalidOperationException>(
            () => sessao.ConfirmPayment(Guid.NewGuid(), Agora));
    }

    // ------------------------------------------------------------- abandono
    [Fact]
    public void QrCodeGeradoENuncaLidoExpiraSozinho()
    {
        var sessao = NovaSessao();
        var tarde = Agora.AddMinutes(StoreSession.QrCodeValidityMinutes + 1);

        Assert.True(sessao.TryExpire(tarde));
        Assert.Equal(SessionStatus.Cancelada, sessao.Status);
    }

    [Fact]
    public void QrCodeDentroDaValidadeNaoExpira()
    {
        var sessao = NovaSessao();

        Assert.False(sessao.TryExpire(Agora.AddMinutes(1)));
        Assert.Equal(SessionStatus.AguardandoEntrada, sessao.Status);
    }

    /// <summary>
    /// Sem isto, uma visita que travou seguiria "ativa" para sempre e o cliente
    /// nunca mais conseguiria gerar um QR code novo — ficaria trancado do lado de fora.
    /// </summary>
    [Fact]
    public void VisitaAbandonadaExpiraEDestravaOCliente()
    {
        var sessao = SessaoAberta();
        var muitoDepois = Agora.AddMinutes(StoreSession.AbandonedVisitMinutes + 1);

        Assert.True(sessao.TryExpire(muitoDepois));
        Assert.Equal(SessionStatus.Cancelada, sessao.Status);
    }

    [Fact]
    public void VisitaDentroDoTempoNaoExpira()
    {
        var sessao = SessaoAberta();
        var poucoDepois = Agora.AddMinutes(StoreSession.AbandonedVisitMinutes - 1);

        Assert.False(sessao.TryExpire(poucoDepois));
        Assert.Equal(SessionStatus.Aberta, sessao.Status);
    }

    [Fact]
    public void SessaoConcluidaNaoExpira()
    {
        var sessao = SessaoAberta();
        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);
        sessao.RequestCheckout(Agora);
        sessao.ConfirmPayment(Guid.NewGuid(), Agora);

        Assert.False(sessao.TryExpire(Agora.AddDays(1)));
        Assert.Equal(SessionStatus.Concluida, sessao.Status);
    }

    // -------------------------------------------------------------- cancelar
    [Fact]
    public void CancelarSessaoAbertaFunciona()
    {
        var sessao = SessaoAberta();

        sessao.Cancel();

        Assert.Equal(SessionStatus.Cancelada, sessao.Status);
    }

    [Fact]
    public void NaoDaParaCancelarSessaoJaConcluida()
    {
        var sessao = SessaoAberta();
        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);
        sessao.RequestCheckout(Agora);
        sessao.ConfirmPayment(Guid.NewGuid(), Agora);

        Assert.Throws<InvalidOperationException>(() => sessao.Cancel());
    }

    [Fact]
    public void NaoDaParaCancelarDuasVezes()
    {
        var sessao = SessaoAberta();
        sessao.Cancel();

        Assert.Throws<InvalidOperationException>(() => sessao.Cancel());
    }

    // ------------------------------------------------------- conta fechando
    [Theory]
    [InlineData(1, 3.50, 3.50)]
    [InlineData(2, 3.50, 7.00)]
    [InlineData(3, 6.90, 20.70)]
    [InlineData(7, 5.55, 38.85)]
    public void OTotalEASomaDosSubtotais(int quantidade, decimal preco, decimal esperado)
    {
        var sessao = SessaoAberta();

        sessao.AddItem(Guid.NewGuid(), "Produto", preco, quantidade);

        Assert.Equal(esperado, sessao.Total);
    }

    [Fact]
    public void ItemsNaoPodeSerAlteradoPorFora()
    {
        var sessao = SessaoAberta();
        sessao.AddItem(Guid.NewGuid(), "Água Mineral 500ml", 3.50m);

        Assert.IsNotType<List<SessionItem>>(sessao.Items);
    }
}
