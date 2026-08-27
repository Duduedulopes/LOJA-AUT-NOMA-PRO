using AutonomousStore.EdgeDesktop.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace AutonomousStore.EdgeDesktop.Services;

public class SessionApiService : ISessionApiService
{
    private readonly HttpClient _httpClient;

    public SessionApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ConfirmEntryResult> ConfirmEntryAsync(string qrCodeToken, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "api/sessions/confirm-entry",
            new ConfirmEntryRequest(qrCodeToken),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var error = ExtractErrorMessage(body) ?? $"Erro ao confirmar entrada ({(int)response.StatusCode}).";
            return new ConfirmEntryResult(Allowed: false, Message: error);
        }

        var dto = await response.Content.ReadFromJsonAsync<ConfirmEntryResponse>(cancellationToken);
        if (dto is null)
            return new ConfirmEntryResult(Allowed: false, Message: "A API retornou resposta vazia.");

        return new ConfirmEntryResult(
            Allowed: dto.Allowed,
            Message: string.IsNullOrWhiteSpace(dto.Message) ? "Entrada liberada." : dto.Message,
            CustomerName: dto.CustomerName,
            SessionId: dto.SessionId,
            EntryConfirmedAt: dto.EntryConfirmedAt);
    }

    public async Task<SessionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/sessions/{id}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionDto>(cancellationToken);
    }

    public async Task<SessionDto> AddItemByRfidAsync(Guid id, string rfidTag, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            $"api/sessions/{id}/items/by-rfid",
            new AddSessionItemByRfidRequest(rfidTag),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(ExtractErrorMessage(body) ?? $"Erro ao registrar leitura ({(int)response.StatusCode}).");
        }

        var session = await response.Content.ReadFromJsonAsync<SessionDto>(cancellationToken);
        return session ?? throw new InvalidOperationException("A API retornou resposta vazia.");
    }

    private static string? ExtractErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var errorProp))
                return errorProp.GetString();
        }
        catch (JsonException)
        {
            // corpo não era JSON — ignora, quem chamou usa a mensagem padrão
        }

        return null;
    }
}
