using System.Net.Http.Json;
using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

public interface IChatApiService
{
    Task<string> SendAsync(List<ChatMessageDto> messages);
}

public class ChatApiService : IChatApiService
{
    private readonly HttpClient _http;

    public ChatApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<string> SendAsync(List<ChatMessageDto> messages)
    {
        var response = await _http.PostAsJsonAsync("api/chat", new ChatRequest(messages));

        if (!response.IsSuccessStatusCode)
            return "Não consegui falar com o assistente agora. Tenta de novo em instantes.";

        var result = await response.Content.ReadFromJsonAsync<ChatResponse>();
        return result?.Reply ?? "Não consegui gerar uma resposta agora.";
    }
}
