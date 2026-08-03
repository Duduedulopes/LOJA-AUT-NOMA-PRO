using System.Net;
using System.Net.Http.Json;
using AutonomousStore.AdminApp.Models;

namespace AutonomousStore.AdminApp.Services;

public interface ISessionApiService
{
    Task<SessionDto?> GetCurrentOpenAsync();
    Task<List<SessionDto>> GetPendingEntryAsync();
    Task<(bool Success, string? Error)> ConfirmEntryAsync(string qrCodeToken);
    Task<List<SessionDto>> GetHistoryAsync();
}

public class SessionApiService : ISessionApiService
{
    private readonly HttpClient _http;

    public SessionApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<SessionDto?> GetCurrentOpenAsync()
    {
        var response = await _http.GetAsync("api/sessions/current-open");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionDto>();
    }

    public async Task<List<SessionDto>> GetPendingEntryAsync()
    {
        var result = await _http.GetFromJsonAsync<List<SessionDto>>("api/sessions/pending-entry");
        return result ?? new();
    }

    /// <summary>Simula a leitora da porta: envia o token do QR code, não o Id da sessão.</summary>
    public async Task<(bool Success, string? Error)> ConfirmEntryAsync(string qrCodeToken)
    {
        var response = await _http.PostAsJsonAsync("api/sessions/confirm-entry", new { qrCodeToken });

        if (response.IsSuccessStatusCode)
            return (true, null);

        try
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return (false, body is not null && body.TryGetValue("error", out var msg) ? msg : $"Erro ({(int)response.StatusCode}).");
        }
        catch
        {
            return (false, $"Erro ({(int)response.StatusCode}).");
        }
    }

    public async Task<List<SessionDto>> GetHistoryAsync()
    {
        var result = await _http.GetFromJsonAsync<List<SessionDto>>("api/sessions/history");
        return result ?? new();
    }
}
