using System.Text.Json;
using System.Text.Json.Serialization;
using AutonomousStore.WebApi.Contracts.Chat;

namespace AutonomousStore.WebApi.Services;

public interface IGeminiChatService
{
    Task<string> GetReplyAsync(List<ChatMessageDto> messages, CancellationToken cancellationToken = default);
}

/// <summary>
/// Agente simples: responde dúvidas gerais sobre a Smart Store usando um texto de instruções
/// fixo (system prompt). Ainda não consulta o banco de dados — isso é uma evolução futura.
/// </summary>
public class GeminiChatService : IGeminiChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    private const string SystemPrompt = """
        Você é o assistente virtual da Smart Store, uma loja de conveniência autônoma (sem caixas).
        Como funciona: o cliente se cadastra no app, gera um QR code que abre a porta da loja, pega os
        produtos que quiser (sensores identificam automaticamente), e ao sair confirma o pagamento no
        app — por Pix (só a chave) ou cartão de crédito/débito. A loja funciona 24 horas.
        Responda de forma breve, simpática e direta, sempre em português do Brasil.
        Se a pergunta não tiver relação com a loja, ou você não souber a resposta, oriente a pessoa a
        entrar em contato com o suporte. Nunca invente informações que você não tem certeza.
        """;

    public GeminiChatService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<string> GetReplyAsync(List<ChatMessageDto> messages, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("COLOQUE_"))
            return "O agente ainda não foi configurado (falta a chave da API do Gemini no appsettings.json).";

        var client = _httpClientFactory.CreateClient("GeminiApi");

        var payload = new GeminiRequestPayload(
            SystemInstruction: new GeminiSystemInstruction([new GeminiPart(SystemPrompt)]),
            Contents: messages.Select(m => new GeminiContent(m.Role, [new GeminiPart(m.Text)])).ToList());

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return $"Não consegui falar com o agente agora (erro {(int)response.StatusCode}). Tenta de novo em instantes.";
        }

        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<GeminiResponsePayload>(responseStream, jsonOptions, cancellationToken);

        var reply = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        return string.IsNullOrWhiteSpace(reply)
            ? "Não consegui gerar uma resposta agora. Tenta reformular a pergunta?"
            : reply;
    }

    private record GeminiRequestPayload(
        [property: JsonPropertyName("systemInstruction")] GeminiSystemInstruction SystemInstruction,
        [property: JsonPropertyName("contents")] List<GeminiContent> Contents);

    private record GeminiSystemInstruction([property: JsonPropertyName("parts")] List<GeminiPart> Parts);

    private record GeminiContent(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("parts")] List<GeminiPart> Parts);

    private record GeminiPart([property: JsonPropertyName("text")] string Text);

    private record GeminiResponsePayload([property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates);

    private record GeminiCandidate([property: JsonPropertyName("content")] GeminiContent? Content);
}