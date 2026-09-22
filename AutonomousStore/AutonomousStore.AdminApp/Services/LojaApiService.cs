using System.Net.Http.Json;
using AutonomousStore.AdminApp.Models;

namespace AutonomousStore.AdminApp.Services;

/// <summary>
/// As lojas da empresa de quem está logado — SÓ LEITURA. Quem cadastra, renomeia, desativa e reativa uma loja é o
/// Criador ou o suporte (é negócio de assinatura, não autoatendimento); o Admin só consulta as que já existem.
/// </summary>
public interface ILojaApiService
{
    Task<(bool Success, LojasDto? Data, string? Error)> ListarAsync();
}

public class LojaApiService : ILojaApiService
{
    private readonly HttpClient _http;

    public LojaApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Success, LojasDto? Data, string? Error)> ListarAsync()
    {
        var response = await _http.GetAsync("api/lojas");
        if (!response.IsSuccessStatusCode)
            return (false, null, await ReadErrorAsync(response));

        return (true, await response.Content.ReadFromJsonAsync<LojasDto>(), null);
    }

    // Mesmo formato dos outros serviços do painel: a API devolve { "error": "..." }, e é essa frase que o Admin lê.
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
