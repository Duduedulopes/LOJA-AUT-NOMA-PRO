using System.Net;
using System.Net.Http.Json;
using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

public class SessionApiService : ISessionApiService
{
    private readonly HttpClient _http;

    public SessionApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<(bool Success, SessionDto? Session, string? Error)> CreateAsync(Guid customerId)
    {
        var response = await _http.PostAsJsonAsync("api/sessions", new CreateSessionRequest(customerId));

        if (response.IsSuccessStatusCode)
        {
            var session = await response.Content.ReadFromJsonAsync<SessionDto>();
            return (true, session, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, SessionDto? Session, string? Error)> RegenerateQrCodeAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/sessions/{id}/regenerate-qrcode", null);

        if (response.IsSuccessStatusCode)
        {
            var session = await response.Content.ReadFromJsonAsync<SessionDto>();
            return (true, session, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    public async Task<SessionDto?> GetActiveAsync(Guid customerId)
    {
        var response = await _http.GetAsync($"api/sessions/active/{customerId}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionDto>();
    }

    public async Task<SessionDto?> GetByIdAsync(Guid id)
    {
        var response = await _http.GetAsync($"api/sessions/{id}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionDto>();
    }

    public async Task<(bool Success, string? Error)> ConfirmEntryAsync(string qrCodeToken)
    {
        var response = await _http.PostAsJsonAsync("api/sessions/confirm-entry", new { qrCodeToken });

        if (response.IsSuccessStatusCode)
            return (true, null);

        return (false, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, SessionDto? Session, string? Error)> CheckoutAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/sessions/{id}/checkout", null);

        if (response.IsSuccessStatusCode)
        {
            var session = await response.Content.ReadFromJsonAsync<SessionDto>();
            return (true, session, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, SessionDto? Session, string? Error)> ConfirmPaymentAsync(Guid id, Guid paymentMethodId)
    {
        var response = await _http.PostAsJsonAsync($"api/sessions/{id}/confirm-payment", new ConfirmPaymentRequest(paymentMethodId));

        if (response.IsSuccessStatusCode)
        {
            var session = await response.Content.ReadFromJsonAsync<SessionDto>();
            return (true, session, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    public async Task<List<SessionDto>> GetHistoryAsync(Guid customerId)
    {
        var result = await _http.GetFromJsonAsync<List<SessionDto>>($"api/sessions/history/{customerId}");
        return result ?? new();
    }

    public async Task<(bool Success, string? Error)> CancelAsync(Guid id)
    {
        var response = await _http.PostAsync($"api/sessions/{id}/cancel", null);
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
