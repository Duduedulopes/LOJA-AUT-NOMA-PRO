using System.Net.Http.Json;
using AutonomousStore.CriadorApp.Models;

namespace AutonomousStore.CriadorApp.Services;

/// <summary>
/// A área do Criador: o painel, as empresas, a equipe de suporte e a auditoria. Tudo aqui fala com <c>/api/platform/*</c> e
/// <c>/api/suporte-auth/register</c> — rotas que só o token do Criador abre.
/// </summary>
public interface IPlatformApiService
{
    Task<(bool Success, PainelDto? Data, string? Error)> PainelAsync();

    Task<(bool Success, List<EmpresaDto>? Data, string? Error)> EmpresasAsync();
    Task<(bool Success, EmpresaDto? Data, string? Error)> CriarEmpresaAsync(CriarEmpresaRequest request);
    Task<(bool Success, EmpresaDto? Data, string? Error)> SuspenderAsync(Guid id);
    Task<(bool Success, EmpresaDto? Data, string? Error)> ReativarAsync(Guid id);
    Task<(bool Success, EmpresaDto? Data, string? Error)> DefinirLimiteDeLojasAsync(Guid id, int limite);
    Task<(bool Success, LojasDaEmpresaDto? Data, string? Error)> LojasDaEmpresaAsync(Guid id);

    /// <summary>Abrir, renomear, desativar e reativar uma loja EM NOME de uma empresa — o Admin dela não faz mais isso.</summary>
    Task<(bool Success, LojaDaEmpresaDto? Data, string? Error)> CriarLojaAsync(Guid empresaId, string nome, string? segmento);
    Task<(bool Success, LojaDaEmpresaDto? Data, string? Error)> AtualizarLojaAsync(Guid empresaId, Guid lojaId, string nome, string? segmento);
    Task<(bool Success, LojaDaEmpresaDto? Data, string? Error)> DesativarLojaAsync(Guid empresaId, Guid lojaId);
    Task<(bool Success, LojaDaEmpresaDto? Data, string? Error)> AtivarLojaAsync(Guid empresaId, Guid lojaId);

    Task<(bool Success, List<TecnicoDto>? Data, string? Error)> TecnicosAsync();
    Task<(bool Success, string? Error)> CadastrarTecnicoAsync(CadastrarTecnicoRequest request);

    Task<(bool Success, List<AuditoriaDto>? Data, string? Error)> AuditoriaAsync(
        DateTime? desde = null, DateTime? ate = null, Guid? empresaId = null, string? acao = null, int limite = 200);
}

public class PlatformApiService : IPlatformApiService
{
    private readonly HttpClient _http;

    public PlatformApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Success, PainelDto? Data, string? Error)> PainelAsync()
        => await LerAsync<PainelDto>(_http.GetAsync("api/platform/painel"));

    public async Task<(bool Success, List<EmpresaDto>? Data, string? Error)> EmpresasAsync()
        => await LerAsync<List<EmpresaDto>>(_http.GetAsync("api/platform/empresas"));

    public async Task<(bool Success, EmpresaDto? Data, string? Error)> CriarEmpresaAsync(CriarEmpresaRequest request)
        => await LerAsync<EmpresaDto>(_http.PostAsJsonAsync("api/platform/empresas", request));

    public async Task<(bool Success, EmpresaDto? Data, string? Error)> SuspenderAsync(Guid id)
        => await LerAsync<EmpresaDto>(_http.PostAsync($"api/platform/empresas/{id}/suspender", content: null));

    public async Task<(bool Success, EmpresaDto? Data, string? Error)> ReativarAsync(Guid id)
        => await LerAsync<EmpresaDto>(_http.PostAsync($"api/platform/empresas/{id}/reativar", content: null));

    public async Task<(bool Success, EmpresaDto? Data, string? Error)> DefinirLimiteDeLojasAsync(Guid id, int limite)
        => await LerAsync<EmpresaDto>(_http.PutAsJsonAsync(
            $"api/platform/empresas/{id}/limite-de-lojas", new DefinirLimiteDeLojasRequest(limite)));

    public async Task<(bool Success, LojasDaEmpresaDto? Data, string? Error)> LojasDaEmpresaAsync(Guid id)
        => await LerAsync<LojasDaEmpresaDto>(_http.GetAsync($"api/platform/empresas/{id}/lojas"));

    public async Task<(bool Success, LojaDaEmpresaDto? Data, string? Error)> CriarLojaAsync(Guid empresaId, string nome, string? segmento)
        => await LerAsync<LojaDaEmpresaDto>(
            _http.PostAsJsonAsync($"api/platform/empresas/{empresaId}/lojas", new SalvarLojaRequest(nome, segmento)));

    public async Task<(bool Success, LojaDaEmpresaDto? Data, string? Error)> AtualizarLojaAsync(
        Guid empresaId, Guid lojaId, string nome, string? segmento)
        => await LerAsync<LojaDaEmpresaDto>(
            _http.PutAsJsonAsync($"api/platform/empresas/{empresaId}/lojas/{lojaId}", new SalvarLojaRequest(nome, segmento)));

    public async Task<(bool Success, LojaDaEmpresaDto? Data, string? Error)> DesativarLojaAsync(Guid empresaId, Guid lojaId)
        => await LerAsync<LojaDaEmpresaDto>(
            _http.PostAsync($"api/platform/empresas/{empresaId}/lojas/{lojaId}/desativar", content: null));

    public async Task<(bool Success, LojaDaEmpresaDto? Data, string? Error)> AtivarLojaAsync(Guid empresaId, Guid lojaId)
        => await LerAsync<LojaDaEmpresaDto>(
            _http.PostAsync($"api/platform/empresas/{empresaId}/lojas/{lojaId}/ativar", content: null));

    public async Task<(bool Success, List<TecnicoDto>? Data, string? Error)> TecnicosAsync()
        => await LerAsync<List<TecnicoDto>>(_http.GetAsync("api/platform/tecnicos"));

    public async Task<(bool Success, string? Error)> CadastrarTecnicoAsync(CadastrarTecnicoRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/suporte-auth/register", request);
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, List<AuditoriaDto>? Data, string? Error)> AuditoriaAsync(
        DateTime? desde = null, DateTime? ate = null, Guid? empresaId = null, string? acao = null, int limite = 200)
    {
        var query = new List<string> { $"limite={limite}" };
        if (desde is { } d) query.Add($"desde={Uri.EscapeDataString(d.ToString("O"))}");
        if (ate is { } a) query.Add($"ate={Uri.EscapeDataString(a.ToString("O"))}");
        if (empresaId is { } e) query.Add($"empresaId={e}");
        if (!string.IsNullOrWhiteSpace(acao)) query.Add($"acao={Uri.EscapeDataString(acao)}");

        return await LerAsync<List<AuditoriaDto>>(_http.GetAsync("api/platform/auditoria?" + string.Join("&", query)));
    }

    // ── apoio ────────────────────────────────────────────────────────────

    private static async Task<(bool Success, T? Data, string? Error)> LerAsync<T>(Task<HttpResponseMessage> envio)
    {
        var response = await envio;
        if (!response.IsSuccessStatusCode)
            return (false, default, await ReadErrorAsync(response));

        return (true, await response.Content.ReadFromJsonAsync<T>(), null);
    }

    // Mesmo formato dos outros apps: a API devolve { "error": ... }, e é essa frase que o Criador lê.
    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return body is not null && body.TryGetValue("error", out var msg) ? msg : $"Erro ({(int)response.StatusCode}).";
        }
        catch
        {
            return $"Erro ({(int)response.StatusCode}).";
        }
    }
}
