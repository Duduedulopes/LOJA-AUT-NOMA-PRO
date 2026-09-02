using System.Net.Http.Json;
using AutonomousStore.SuporteApp.Models;

namespace AutonomousStore.SuporteApp.Services;

public interface ISuporteAuthApiService
{
    Task<(bool Success, SuporteAuthResponse? Response, string? Error)> LoginAsync(string email, string password);
    Task<(bool Success, SuporteAuthResponse? Response, string? Error)> RegisterAsync(
        string name, string email, string phoneNumber, string cpf,
        string password, string confirmPassword);
}

public class SuporteAuthApiService : ISuporteAuthApiService
{
    private readonly HttpClient _http;

    public SuporteAuthApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Success, SuporteAuthResponse? Response, string? Error)> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("api/suporte-auth/login", new SuporteLoginRequest(email, password));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var error = body is not null && body.TryGetValue("error", out var msg) ? msg : "E-mail ou senha inválidos.";
            return (false, null, error);
        }

        var result = await response.Content.ReadFromJsonAsync<SuporteAuthResponse>();
        return (true, result, null);
    }

    public async Task<(bool Success, SuporteAuthResponse? Response, string? Error)> RegisterAsync(
        string name, string email, string phoneNumber, string cpf,
        string password, string confirmPassword)
    {
        // Argumentos NOMEADOS: o record e posicional e tem quatro strings
        // seguidas. Trocar duas de lugar compila sem uma palavra e grava o
        // telefone no campo do CPF — erro que so aparece muito depois, na
        // hora de ligar para alguem.
        var response = await _http.PostAsJsonAsync("api/suporte-auth/register",
            new SuporteRegisterRequest(
                Name: name,
                Email: email,
                PhoneNumber: phoneNumber,
                Cpf: cpf,
                Password: password,
                ConfirmPassword: confirmPassword));

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            var error = body is not null && body.TryGetValue("error", out var msg) ? msg : "Erro ao criar conta.";
            return (false, null, error);
        }

        var result = await response.Content.ReadFromJsonAsync<SuporteAuthResponse>();
        return (true, result, null);
    }
}
