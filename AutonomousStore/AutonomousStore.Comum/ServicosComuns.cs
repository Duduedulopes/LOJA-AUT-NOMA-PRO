using AutonomousStore.Comum.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AutonomousStore.Comum;

/// <summary>O que os três apps registram para ter a conversa de suporte.</summary>
/// <remarks>
/// Mesmo desenho do `ServicosDoGerente`: a lista mora aqui, e não copiada em
/// três `Program.cs`. O `HttpClient` continua vindo de cada app — é ele que
/// carrega o token de quem está logado, e o token do cliente não é o do
/// admin nem o do técnico.
/// </remarks>
public static class ServicosComuns
{
    /// <param name="lerToken">
    /// Como ACHAR o token de quem está logado, neste app.
    /// </param>
    /// <remarks>
    /// POR QUE O TOKEN PRECISA SER PEDIDO ASSIM, e não lido daqui.
    ///
    /// Para as chamadas HTTP normais esta biblioteca nunca precisou saber de
    /// token: o `AuthHeaderHandler` de cada app põe o cabeçalho antes da
    /// requisição sair, e o `HttpClient` chega aqui já autenticado.
    ///
    /// O WebSocket quebra esse arranjo. Ele não passa por `DelegatingHandler`
    /// nenhum, e o navegador não deixa pôr cabeçalho numa conexão WebSocket —
    /// então o token tem de ser LIDO e posto na URL na hora de conectar.
    ///
    /// Só que cada app guarda o dele numa classe `AppState` própria, no
    /// próprio namespace, e esta biblioteca é magra de propósito: ela não
    /// referencia app nenhum. A saída é o app entregar a pergunta já
    /// respondida — uma função de uma linha, escrita lá, chamada daqui.
    ///
    /// É lido A CADA CONEXÃO, e não guardado: quem faz logout e entra com
    /// outra conta não pode continuar ouvindo a conversa da anterior.
    /// </remarks>
    public static IServiceCollection AdicionarChamados(
        this IServiceCollection servicos,
        Func<IServiceProvider, string?> lerToken)
    {
        servicos.AddScoped<IChamadoApiService, ChamadoApiService>();
        servicos.AddScoped(sp => new TokenDeQuemEsta(() => lerToken(sp)));
        return servicos;
    }
}
