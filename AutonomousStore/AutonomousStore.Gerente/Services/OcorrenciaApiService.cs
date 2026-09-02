using System.Net.Http.Json;
using System.Text;
using AutonomousStore.Gerente.Models;

namespace AutonomousStore.Gerente.Services;

public interface IOcorrenciaApiService
{
    Task<List<OcorrenciaDto>?> BuscarAsync(
        DateTime? desde = null,
        DateTime? ate = null,
        string? tipo = null,
        string? severidadeMinima = null,
        string? estado = null,
        Guid? correlationId = null,
        int limite = 200);

    Task<OcorrenciaDto?> PorIdAsync(Guid id);

    Task<OcorrenciaDto?> MarcarVistaAsync(Guid id);

    Task<OcorrenciaDto?> ResolverAsync(Guid id, string? nota);

    Task<OcorrenciaDto?> EnviarAoSuporteAsync(Guid id, string? descricaoDoAdmin);

    Task<NaoVistasDto?> NaoVistasAsync();
}

public class OcorrenciaApiService : IOcorrenciaApiService
{
    private readonly HttpClient _http;

    public OcorrenciaApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<OcorrenciaDto>?> BuscarAsync(
        DateTime? desde = null,
        DateTime? ate = null,
        string? tipo = null,
        string? severidadeMinima = null,
        string? estado = null,
        Guid? correlationId = null,
        int limite = 200)
    {
        var q = new List<string>();
        var ci = System.Globalization.CultureInfo.InvariantCulture;
        if (desde is { } d) q.Add($"desde={Uri.EscapeDataString(d.ToString("o", ci))}");
        if (ate is { } a) q.Add($"ate={Uri.EscapeDataString(a.ToString("o", ci))}");
        if (tipo is { Length: > 0 }) q.Add($"tipo={Uri.EscapeDataString(tipo)}");
        if (severidadeMinima is { Length: > 0 }) q.Add($"severidade={Uri.EscapeDataString(severidadeMinima)}");
        if (estado is { Length: > 0 }) q.Add($"estado={Uri.EscapeDataString(estado)}");
        if (correlationId is { } cid) q.Add($"correlationId={cid}");
        q.Add($"limite={limite.ToString(ci)}");

        try
        {
            var r = await _http.GetAsync("api/ocorrencias?" + string.Join("&", q));
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<List<OcorrenciaDto>>() ?? new List<OcorrenciaDto>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<OcorrenciaDto?> PorIdAsync(Guid id)
    {
        try
        {
            var r = await _http.GetAsync($"api/ocorrencias/{id}");
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<OcorrenciaDto>();
        }
        catch { return null; }
    }

    public async Task<OcorrenciaDto?> MarcarVistaAsync(Guid id)
    {
        try
        {
            var r = await _http.PostAsync($"api/ocorrencias/{id}/vista", new StringContent("", Encoding.UTF8, "application/json"));
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<OcorrenciaDto>();
        }
        catch { return null; }
    }

    public async Task<OcorrenciaDto?> ResolverAsync(Guid id, string? nota)
    {
        try
        {
            var body = new { nota };
            var r = await _http.PostAsJsonAsync($"api/ocorrencias/{id}/resolver", body);
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<OcorrenciaDto>();
        }
        catch { return null; }
    }

    public async Task<OcorrenciaDto?> EnviarAoSuporteAsync(Guid id, string? descricaoDoAdmin)
    {
        try
        {
            var body = new { descricaoDoAdmin };
            var r = await _http.PostAsJsonAsync($"api/ocorrencias/{id}/suporte", body);
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<OcorrenciaDto>();
        }
        catch { return null; }
    }

    public async Task<NaoVistasDto?> NaoVistasAsync()
    {
        try
        {
            var r = await _http.GetAsync("api/ocorrencias/nao-vistas");
            if (!r.IsSuccessStatusCode) return null;
            return await r.Content.ReadFromJsonAsync<NaoVistasDto>();
        }
        catch
        {
            return null;
        }
    }
}
