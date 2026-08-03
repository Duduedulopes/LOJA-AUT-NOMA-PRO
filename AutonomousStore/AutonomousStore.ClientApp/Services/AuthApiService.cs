using System.Net.Http.Json;
using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

public class AuthApiService : IAuthApiService
{
    private readonly HttpClient _http;

    public AuthApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Success, AuthResponse? Result, string? Error)> RegisterAsync(RegisterRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/register", request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return (true, result, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, AuthResponse? Result, string? Error)> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return (true, result, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, AuthResponse? Result, bool NeedsCpf, PendingGoogleSignup? Pending, string? Error)> GoogleLoginAsync(string idToken)
    {
        var response = await _http.PostAsJsonAsync("api/auth/google", new GoogleLoginRequest(idToken));

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return (true, result, false, null, null);
        }

        if ((int)response.StatusCode == 409)
        {
            var conflict = await response.Content.ReadFromJsonAsync<GoogleNeedsCpfResponse>();

            if (conflict is not null && conflict.Error == "NEEDS_CPF")
            {
                var pending = new PendingGoogleSignup(conflict.Email, conflict.Name, conflict.GoogleId);
                return (false, null, true, pending, null);
            }
        }

        return (false, null, false, null, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, AuthResponse? Result, string? Error)> CompleteGoogleRegistrationAsync(CompleteGoogleRegistrationRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/auth/google/complete", request);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            return (true, result, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
    {
        var response = await _http.PostAsJsonAsync("api/auth/forgot-password", new ForgotPasswordRequest(email));
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword)
    {
        var response = await _http.PostAsJsonAsync("api/auth/reset-password", new ResetPasswordRequest(email, token, newPassword));
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return body is not null && body.TryGetValue("error", out var msg)
                ? msg
                : $"Erro inesperado ({(int)response.StatusCode}).";
        }
        catch
        {
            return $"Erro inesperado ({(int)response.StatusCode}).";
        }
    }
}
