using System.Net.Http.Json;
using AutonomousStore.Gerente.Models;

namespace AutonomousStore.Gerente.Services;

public interface IEspacialApiService
{
    /// <summary>A planta. Buscada uma vez e guardada — ela quase nunca muda.</summary>
    Task<PlantaDto?> PlantaAsync();

    /// <summary>O estado do gêmeo agora. `null` quando o monitor não respondeu.</summary>
    Task<EspacialEstadoDto?> EstadoAsync();

    /// <summary>Quais câmeras estão publicando quadro, e há quanto tempo.</summary>
    Task<List<CameraAoVivoDto>?> CamerasAoVivoAsync();

    /// <summary>A URL do último quadro de uma câmera, já com quebra de cache.</summary>
    string UrlDoQuadro(string papel);
}

/// <summary>
/// Lê o Sistema Espacial SO através do monitor local.
/// </summary>
/// <remarks>
/// SEM CREDENCIAL, igual ao `GerenteEspacialService` — mesmo motivo. O token
/// de admin vale para a WebApi e para mais nada; mandá-lo a um segundo
/// servidor aumentaria a superfície de vazamento sem resolver nada, porque o
/// monitor só devolve dado anônimo: posição no chão, estado de câmera.
///
/// FALHAR AQUI É NORMAL. Na maior parte do tempo o Sistema Espacial não está
/// rodando — ele exige as câmeras plugadas. Por isso tudo devolve `null` em
/// vez de estourar, e a tela diz "não estou vendo o Espacial" em vez de
/// quebrar o painel inteiro.
/// </remarks>
public class EspacialApiService : IEspacialApiService
{
    private readonly IHttpClientFactory _fabrica;
    private PlantaDto? _planta;

    public EspacialApiService(IHttpClientFactory fabrica) => _fabrica = fabrica;

    private HttpClient Monitor()
    {
        var http = _fabrica.CreateClient("MonitorGerente");
        // Dois segundos: é uma tela que se atualiza sozinha. Esperar mais que
        // isso trava o desenho esperando um servidor que provavelmente não
        // está no ar — e a tela precisa poder DIZER que não está.
        http.Timeout = TimeSpan.FromSeconds(2);
        return http;
    }

    public async Task<PlantaDto?> PlantaAsync()
    {
        // Guardada depois da primeira leitura. A planta muda quando a loja
        // muda de layout; buscá-la a cada atualização de tela seria mandar a
        // mesma gôndola pela rede cinco vezes por segundo.
        if (_planta is not null) return _planta;

        try
        {
            _planta = await Monitor().GetFromJsonAsync<PlantaDto>("api/planta");
        }
        catch
        {
            _planta = null;
        }
        return _planta;
    }

    /// <summary>Esquece a planta guardada — para quando a loja for recalibrada.</summary>
    public void EsquecerPlanta() => _planta = null;

    public async Task<EspacialEstadoDto?> EstadoAsync()
    {
        try
        {
            var pacote = await Monitor().GetFromJsonAsync<PacoteDeEstado>("api/estado");
            return pacote?.Espacial;
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<CameraAoVivoDto>?> CamerasAoVivoAsync()
    {
        try
        {
            var r = await Monitor().GetFromJsonAsync<CamerasAoVivoDto>("api/cameras");
            return r?.Cameras ?? new List<CameraAoVivoDto>();
        }
        catch
        {
            return null;
        }
    }

    /// <remarks>
    /// A QUEBRA DE CACHE NÃO É OPCIONAL. O caminho é sempre o mesmo
    /// (`alto.jpg`), então o navegador serve a primeira imagem para sempre e
    /// a câmera parece congelada — sem erro nenhum, o que é pior: a tela
    /// mostra uma cena de dez minutos atrás com cara de agora.
    /// </remarks>
    public string UrlDoQuadro(string papel)
    {
        var b = _fabrica.CreateClient("MonitorGerente").BaseAddress;
        var carimbo = DateTime.UtcNow.Ticks.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return $"{b}api/camera/{Uri.EscapeDataString(papel)}.jpg?t={carimbo}";
    }

    /// <summary>O `/api/estado` devolve três coisas; aqui só interessa a espacial.</summary>
    private record PacoteDeEstado(
        [property: System.Text.Json.Serialization.JsonPropertyName("espacial")]
        EspacialEstadoDto? Espacial);
}
