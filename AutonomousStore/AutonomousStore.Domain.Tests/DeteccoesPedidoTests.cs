using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Tests;

/// <summary>
/// Quem pode abrir um pedido ao suporte. A lista de apps e fechada de proposito
/// (a rota que recebe relatos de erro e anonima), entao um app novo que esquece
/// de entrar nela so descobre o problema quando alguem tenta pedir ajuda.
/// </summary>
public class DeteccoesPedidoTests
{
    // ClientApp e AdminApp PEDEM ajuda: o pedido cai na fila do suporte. SuporteApp e
    // CriadorApp SAO o suporte: quem abre ja esta atendendo, entao o pedido nasce em analise.
    [Theory]
    [InlineData("ClientApp", EstadoDaOcorrencia.NoSuporte, AutorDaMensagem.Cliente)]
    [InlineData("AdminApp", EstadoDaOcorrencia.NoSuporte, AutorDaMensagem.Admin)]
    [InlineData("SuporteApp", EstadoDaOcorrencia.EmAnalise, AutorDaMensagem.Suporte)]
    [InlineData("CriadorApp", EstadoDaOcorrencia.EmAnalise, AutorDaMensagem.Suporte)]   // o 4o app: sem estar na lista, o Criador nao conseguia abrir chamado
    public void CadaAppDoSistemaConsegueAbrirUmPedido(string app, EstadoDaOcorrencia estado, AutorDaMensagem autor)
    {
        var pedido = Deteccoes.PedidoAoSuporte(
            app, ehMudanca: false, assunto: "Ajuda", texto: "Preciso de ajuda",
            quemNome: "Fulano", quemEmail: "fulano@exemplo.com", paginaOndeEstava: "/", quandoUtc: DateTime.UtcNow);

        Assert.Equal(app, pedido.Sistema);
        Assert.Equal(estado, pedido.Estado);
        Assert.Equal(autor, pedido.Mensagens.Single().Autor);
    }

    [Fact]
    public void AppQueNaoExisteContinuaSendoRecusado()
    {
        Assert.Throws<ArgumentException>(() => Deteccoes.PedidoAoSuporte(
            "BancoCentral", false, "x", "y", "Fulano", "fulano@exemplo.com", "/", DateTime.UtcNow));
    }
}
