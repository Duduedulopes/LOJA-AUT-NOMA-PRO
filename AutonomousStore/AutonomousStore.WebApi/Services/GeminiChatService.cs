using System.Text.Json;
using System.Text.Json.Serialization;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Chat;

namespace AutonomousStore.WebApi.Services;

public interface IGeminiChatService
{
    Task<string> GetReplyAsync(List<ChatMessageDto> messages, CancellationToken cancellationToken = default);
}

/// <summary>
/// Agente simples: responde dúvidas gerais sobre a AutonomousStore usando um texto de instruções
/// fixo (system prompt). Ainda não consulta o banco de dados — isso é uma evolução futura.
/// </summary>
public class GeminiChatService : IGeminiChatService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IRegistradorDeOcorrencia _ocorrencias;
    private readonly ILogger<GeminiChatService> _log;

    /// <summary>O endereço, num lugar só — é ele que o 404 acusa.</summary>
    /// <remarks>
    /// APELIDO, E NÃO VERSÃO CRAVADA — e isso mudou depois de um 404.
    ///
    /// Este serviço pedia `gemini-2.5-flash`, cravado. O `GeminiVisionService`,
    /// ao lado, sempre pediu `gemini-flash-latest`. Dois serviços que falam com
    /// o MESMO fornecedor, com a MESMA chave, seguindo políticas diferentes —
    /// e só um foi atualizado quando o modelo mudou. Foi assim que o
    /// assistente do cliente parou sozinho, sem ninguém ter mexido nele.
    ///
    /// O apelido `-latest` acompanha o que o Google estiver servindo. A troca
    /// tem um preço honesto: a resposta pode mudar de tom de um dia para o
    /// outro, sem aviso. Para um assistente que responde dúvida de cliente,
    /// isso pesa muito menos do que ele simplesmente parar de existir — que é
    /// o que a versão cravada garante no dia da aposentadoria.
    ///
    /// Se um dia o tom importar mais que a continuidade, é aqui que se crava
    /// de novo: uma linha, e a data de validade volta junto.
    /// </remarks>
    private const string Modelo = "gemini-flash-latest";
    private const string Endereco =
        "https://generativelanguage.googleapis.com/v1beta/models/" + Modelo + ":generateContent";

    /// <summary>Textos que são claramente "ainda não preenchi isto aqui".</summary>
    /// <remarks>
    /// A checagem antiga era só `StartsWith("COLOQUE_")`, e o
    /// `appsettings.json` deste projeto traz `SUA_CHAVE...`. Ou seja: o
    /// placeholder passava pela guarda e era MANDADO ao Google como se fosse
    /// chave. A resposta vinha errada e não dizia por quê.
    /// </remarks>
    private static readonly string[] NaoEChave = { "COLOQUE_", "SUA_", "SEU_", "INSIRA", "CHAVE_AQUI", "TODO" };

    private const string SystemPrompt = """
        Você é o assistente virtual da AutonomousStore, uma loja de conveniência autônoma (sem caixas).
        Como funciona: o cliente se cadastra no app, gera um QR code que abre a porta da loja, pega os
        produtos que quiser (sensores identificam automaticamente), e ao sair confirma o pagamento no
        app — por Pix (só a chave) ou cartão de crédito/débito. A loja funciona 24 horas.
        Responda de forma breve, simpática e direta, sempre em português do Brasil.
        Se a pergunta não tiver relação com a loja, ou você não souber a resposta, oriente a pessoa a
        entrar em contato com o suporte. Nunca invente informações que você não tem certeza.
        """;

    public GeminiChatService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IRegistradorDeOcorrencia ocorrencias,
        ILogger<GeminiChatService> log)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _ocorrencias = ocorrencias;
        _log = log;
    }

    public async Task<string> GetReplyAsync(List<ChatMessageDto> messages, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["Gemini:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey) ||
            NaoEChave.Any(x => apiKey.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
        {
            _log.LogWarning(
                "O assistente foi chamado sem chave do Gemini configurada (Gemini:ApiKey).");
            return "O agente ainda não foi configurado (falta a chave da API do Gemini no appsettings.json).";
        }

        var client = _httpClientFactory.CreateClient("GeminiApi");

        var conversa = Arrumar(messages);

        if (conversa.Count == 0)
            return "Não entendi a pergunta. Pode escrever de novo?";

        var payload = new GeminiRequestPayload(
            SystemInstruction: new GeminiSystemInstruction([new GeminiPart(SystemPrompt)]),
            Contents: conversa);

        var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endereco);
        request.Headers.Add("x-goog-api-key", apiKey);
        request.Content = new StringContent(
            JsonSerializer.Serialize(payload, jsonOptions),
            System.Text.Encoding.UTF8,
            "application/json");

        var response = await client.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // O CORPO DA RESPOSTA É A METADE QUE FALTAVA.
            //
            // Antes daqui, o texto que o Google manda de volta — que diz
            // exatamente qual é o problema, "model not found", "API key not
            // valid", o que for — ia direto para o lixo. Sobrava o número, e
            // número sozinho não conserta nada: para descobrir o motivo era
            // preciso ir mexer no código.
            var corpo = await LerCorpo(response, cancellationToken);

            _log.LogError("Gemini respondeu {Status} em {Modelo}. Resposta: {Corpo}",
                (int)response.StatusCode, Modelo, corpo);

            // E vira ocorrência, para aparecer no histórico do suporte junto
            // com todo o resto. Falha de serviço externo que não deixa rastro
            // é a que mais custa para achar.
            await _ocorrencias.RegistrarAsync(
                Deteccoes.FalhaDeIntegracao(
                    servico: "Gemini",
                    operacao: $"POST {Modelo}:generateContent",
                    status: (int)response.StatusCode,
                    corpoDaResposta: corpo,
                    correlationId: Guid.NewGuid(),
                    quandoUtc: DateTime.UtcNow),
                cancellationToken);

            return "Não consegui falar com o agente agora. Já registrei o problema — tenta de novo em instantes.";
        }

        var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var result = await JsonSerializer.DeserializeAsync<GeminiResponsePayload>(responseStream, jsonOptions, cancellationToken);

        var reply = result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;

        return string.IsNullOrWhiteSpace(reply)
            ? "Não consegui gerar uma resposta agora. Tenta reformular a pergunta?"
            : reply;
    }

    /// <summary>Deixa a conversa no formato que o Gemini aceita.</summary>
    /// <remarks>
    /// A CONVERSA TEM DE COMEÇAR COM O USUÁRIO. Foi isto que quebrou o
    /// assistente do cliente.
    ///
    /// O widget guarda a saudação ("Olá! Sou o assistente…") como se fosse
    /// uma fala do modelo, e mandava a lista inteira para cá. Então todo
    /// pedido chegava começando por `model` — o que o Gemini recusa. E a cada
    /// tentativa a mensagem de erro entrava como histórico também, deixando a
    /// conversa mais suja a cada rodada.
    ///
    /// O widget foi corrigido do lado dele. Esta guarda fica assim mesmo, e
    /// não é redundância: é o SERVIDOR que fala com o Gemini, e ele é o único
    /// lugar onde a regra vale para qualquer cliente que venha a existir —
    /// incluindo o próximo, escrito por outra pessoa, que vai cometer o mesmo
    /// engano.
    ///
    /// O papel também é normalizado. "assistant" é o nome que o mundo OpenAI
    /// usa; aqui é "model". Aceitar os dois custa uma linha e evita um erro
    /// que ninguém liga à causa.
    /// </remarks>
    private static List<GeminiContent> Arrumar(List<ChatMessageDto> messages)
    {
        static string Papel(string? r) =>
            string.Equals(r, "user", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "human", StringComparison.OrdinalIgnoreCase)
                ? "user" : "model";

        return messages
            .Where(m => !string.IsNullOrWhiteSpace(m.Text))
            // Fora tudo o que vem ANTES da primeira fala do usuário.
            .SkipWhile(m => Papel(m.Role) != "user")
            .Select(m => new GeminiContent(Papel(m.Role), [new GeminiPart(m.Text)]))
            .ToList();
    }

    /// <summary>Lê o corpo do erro sem deixar que a leitura vire outro erro.</summary>
    private static async Task<string> LerCorpo(HttpResponseMessage r, CancellationToken c)
    {
        try { return await r.Content.ReadAsStringAsync(c); }
        catch (Exception e) { return $"(não consegui ler a resposta: {e.Message})"; }
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