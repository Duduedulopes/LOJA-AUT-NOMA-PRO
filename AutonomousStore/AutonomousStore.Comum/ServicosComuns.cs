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
    public static IServiceCollection AdicionarChamados(this IServiceCollection servicos)
    {
        servicos.AddScoped<IChamadoApiService, ChamadoApiService>();
        return servicos;
    }
}
