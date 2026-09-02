using System.Net.Http.Json;
using AutonomousStore.Comum.Models;

namespace AutonomousStore.Comum.Services;

public interface IChamadoApiService
{
    Task<ChamadoDto?> AbrirAsync(string assunto, string texto, bool ehMudanca, string? pagina);
    Task<List<ChamadoDto>?> MeusAsync();
    Task<ChamadoDto?> UmAsync(Guid id);
    Task<ChamadoDto?> ResponderAsync(Guid id, string texto);
}

/// <summary>
/// Os chamados, do lado do navegador.
/// </summary>
/// <remarks>
/// TUDO DEVOLVE `null` EM VEZ DE ESTOURAR, e isso é decisão e não preguiça.
///
/// Este serviço roda dentro de três aplicativos Blazor. Exceção que sobe daqui
/// vira barra vermelha e derruba a tela — e a tela que ela derruba é
/// justamente a de pedir ajuda. Uma pessoa que não conseguiu falar com o
/// suporte precisa ver "não consegui enviar, tenta de novo", não uma tela
/// quebrada.
///
/// Quem chama distingue os dois casos pelo retorno: `null` é falha, objeto é
/// sucesso. A tela decide o que dizer.
/// </remarks>
public class ChamadoApiService : IChamadoApiService
{
    private readonly HttpClient _http;

    public ChamadoApiService(HttpClient http) => _http = http;

    public async Task<ChamadoDto?> AbrirAsync(string assunto, string texto, bool ehMudanca, string? pagina)
    {
        try
        {
            var r = await _http.PostAsJsonAsync("api/chamados",
                new AbrirChamadoDto(assunto, texto, ehMudanca, pagina));
            return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<ChamadoDto>() : null;
        }
        catch { return null; }
    }

    public async Task<List<ChamadoDto>?> MeusAsync()
    {
        try { return await _http.GetFromJsonAsync<List<ChamadoDto>>("api/chamados/meus"); }
        catch { return null; }
    }

    public async Task<ChamadoDto?> UmAsync(Guid id)
    {
        try { return await _http.GetFromJsonAsync<ChamadoDto>($"api/chamados/{id}"); }
        catch { return null; }
    }

    public async Task<ChamadoDto?> ResponderAsync(Guid id, string texto)
    {
        try
        {
            var r = await _http.PostAsJsonAsync($"api/chamados/{id}/mensagens", new { texto });
            return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<ChamadoDto>() : null;
        }
        catch { return null; }
    }
}
