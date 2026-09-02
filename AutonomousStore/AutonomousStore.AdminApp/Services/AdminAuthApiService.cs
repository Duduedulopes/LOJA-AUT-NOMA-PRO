using System.Net.Http.Json;
using AutonomousStore.AdminApp.Models;
using AutonomousStore.Gerente.Models;

namespace AutonomousStore.AdminApp.Services;

public interface IAdminAuthApiService
{
    Task<(bool Success, AdminAuthResponse? Response, string? Error)> LoginAsync(string email, string password);
}

public class AdminAuthApiService : IAdminAuthApiService
{
    private readonly HttpClient _http;

    public AdminAuthApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Success, AdminAuthResponse? Response, string? Error)> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/admin-auth/login", new AdminLoginRequest(email, password));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var error = body is not null && body.TryGetValue("error", out var msg) ? msg : "E-mail ou senha inválidos.";
            return (false, null, error);
        }

        var result = await response.Content.ReadFromJsonAsync<AdminAuthResponse>();
        return (true, result, null);
    }
}
