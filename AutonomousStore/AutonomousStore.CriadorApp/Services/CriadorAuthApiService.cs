using System.Net.Http.Json;
using AutonomousStore.CriadorApp.Models;

namespace AutonomousStore.CriadorApp.Services;

public interface ICriadorAuthApiService
{
    Task<(bool Success, CriadorAuthResponse? Response, string? Error)> LoginAsync(string email, string password);
}

public class CriadorAuthApiService : ICriadorAuthApiService
{
    private readonly HttpClient _http;

    public CriadorAuthApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Success, CriadorAuthResponse? Response, string? Error)> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/criador-auth/login", new CriadorLoginRequest(email, password));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var error = body is not null && body.TryGetValue("error", out var msg) ? msg : "E-mail ou senha inválidos.";
            return (false, null, error);
        }

        var result = await response.Content.ReadFromJsonAsync<CriadorAuthResponse>();
        return (true, result, null);
    }
}
