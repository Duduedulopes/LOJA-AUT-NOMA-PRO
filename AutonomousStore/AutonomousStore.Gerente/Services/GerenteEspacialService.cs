using System.Net.Http;
using System.Net.Http.Json;
using AutonomousStore.Gerente.Models;

namespace AutonomousStore.Gerente.Services;

public interface IGerenteEspacialService
{
    Task<EspacialResumoDto?> ObterAsync();
}

/// <summary>
/// Le o resumo espacial do monitor local.
/// </summary>
/// <remarks>
/// ESTE SERVICO NAO MANDA CREDENCIAL NENHUMA, E ISSO E DELIBERADO.
///
/// Ele usa um HttpClient PROPRIO, sem o AuthHeaderHandler. O token de admin
/// vale para a WebApi do AutonomousStore e para mais nada — mandar ele a um segundo
/// servidor so aumentaria a superficie de vazamento sem resolver problema
/// nenhum, porque o monitor so devolve dado anonimo.
///
/// Falhar aqui e NORMAL: na maior parte do tempo o monitor nao esta rodando.
/// Por isso devolve `null` em vez de estourar, e o chat responde que a parte
/// espacial esta indisponivel em vez de quebrar a tela inteira.
/// </remarks>
public class GerenteEspacialService : IGerenteEspacialService
{
    private readonly IHttpClientFactory _fabrica;

    public GerenteEspacialService(IHttpClientFactory fabrica) => _fabrica = fabrica;

    public async Task<EspacialResumoDto?> ObterAsync()
    {
        try
        {
            var http = _fabrica.CreateClient("MonitorGerente");
            http.Timeout = TimeSpan.FromSeconds(2);
            return await http.GetFromJsonAsync<EspacialResumoDto>("api/gerente/espacial");
        }
        catch
        {
            // Monitor fora do ar, porta trocada, CORS negado. Nenhum destes
            // e motivo para o painel administrativo parar de funcionar.
            return null;
        }
    }
}
