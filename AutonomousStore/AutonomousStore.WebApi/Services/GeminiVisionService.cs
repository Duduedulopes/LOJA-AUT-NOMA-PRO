using System.Text.Json;
using System.Text.Json.Serialization;
using AutonomousStore.Domain.Entities;

namespace AutonomousStore.WebApi.Services;

public record ShelfChangeResult(string Action, string? ProductName);

public interface IGeminiVisionService
{
    /// <summary>
    /// Compara duas fotos da mesma prateleira (antes/depois) e determina se um produto foi
    /// retirado, devolvido, ou se não houve mudança real (ex: só uma mão passando).
    /// </summary>
    Task<ShelfChangeResult> AnalyzeShelfChangeAsync(
        string beforeImageBase64,
        string afterImageBase64,
        List<Product> candidateProducts,
        CancellationToken cancellationToken = default);
}

public class GeminiVisionService : IGeminiVisionService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiVisionService> _logger;

    public GeminiVisionService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GeminiVisionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ShelfChangeResult> AnalyzeShelfChangeAsync(
        string beforeImageBase64,
        string afterImageBase64,
        List<Product> candidateProducts,
        CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("COLOQUE_"))
            return new ShelfChangeResult("nenhuma", null);

        var productList = candidateProducts.Count == 0
            ? "(nenhum produto cadastrado)"
            : string.Join(", ", candidateProducts.Select(p => $"\"{p.Name}\""));

        var promptText = $$"""
            Você está analisando duas fotos da mesma prateleira de uma loja autônoma, tiradas com
            poucos segundos de diferença. A primeira imagem é o "antes", a segunda é o "depois".

            Produtos possíveis nessa prateleira: {{productList}}

            Compare as duas imagens e determine exatamente uma destas situações:
            - Um produto da lista foi RETIRADO da prateleira (o cliente pegou o item)
            - Um produto da lista foi DEVOLVIDO à prateleira (o cliente colocou de volta)
            - Não houve mudança real de produto (ex: só uma mão passando, sombra, tremida de câmera,
              ou qualquer coisa que não seja um produto sendo definitivamente retirado ou devolvido)

            Responda ESTRITAMENTE em JSON, sem nenhum texto antes ou depois, neste formato exato:
            {"action": "retirado" | "devolvido" | "nenhuma", "produto": "nome exato de um item da lista, ou null"}
            """;

        var client = _httpClientFactory.CreateClient("GeminiApi");

        var payload = new GeminiVisionPayload(
            Contents:
            [
                new GeminiContent("user",
                [
                    new GeminiPart(promptText, null),
                    new GeminiPart(null, new GeminiInlineData("image/jpeg", StripDataUrlPrefix(beforeImageBase64))),
                    new GeminiPart(null, new GeminiInlineData("image/jpeg", StripDataUrlPrefix(afterImageBase64)))
                ])
            ]);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
           "https://generativelanguage.googleapis.com/v1beta/models/gemini-flash-latest:generateContent");
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("Gemini Vision retornou erro {StatusCode}: {Body}", (int)response.StatusCode, errorBody);
            return new ShelfChangeResult("nenhuma", null);
        }

        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<GeminiVisionResponse>(responseStream, jsonOptions, cancellationToken);

        var text = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        _logger.LogInformation("Gemini Vision respondeu: {RawText}", text ?? "(vazio)");

        return ParseShelfChangeResult(text);
    }

    private ShelfChangeResult ParseShelfChangeResult(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new ShelfChangeResult("nenhuma", null);

        // O Gemini às vezes envolve o JSON em ```json ... ``` mesmo quando instruído a não fazer isso.
        var cleaned = text.Trim().Trim('`');
        if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned[4..].Trim();

        try
        {
            var parsed = JsonSerializer.Deserialize<ShelfChangeJson>(cleaned, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return new ShelfChangeResult(parsed?.Action ?? "nenhuma", parsed?.Produto);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Não consegui interpretar como JSON a resposta do Gemini: {Cleaned}", cleaned);
            return new ShelfChangeResult("nenhuma", null);
        }
    }

    private static string StripDataUrlPrefix(string base64)
    {
        var commaIndex = base64.IndexOf(',');
        return commaIndex >= 0 ? base64[(commaIndex + 1)..] : base64;
    }

    private record ShelfChangeJson(string? Action, string? Produto);

    private record GeminiVisionPayload([property: JsonPropertyName("contents")] List<GeminiContent> Contents);

    private record GeminiContent(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("parts")] List<GeminiPart> Parts);

    private record GeminiPart(
        [property: JsonPropertyName("text")] string? Text,
        [property: JsonPropertyName("inlineData")] GeminiInlineData? InlineData);

    private record GeminiInlineData(
        [property: JsonPropertyName("mimeType")] string MimeType,
        [property: JsonPropertyName("data")] string Data);

    private record GeminiVisionResponse([property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates);

    private record GeminiCandidate([property: JsonPropertyName("content")] GeminiContent? Content);
}