namespace AutonomousStore.WebApi.Contracts.Chat;

// Role: "user" (o cliente) ou "model" (o agente) — mesmos nomes que a API do Gemini espera.
public record ChatMessageDto(string Role, string Text);

public record ChatRequest(List<ChatMessageDto> Messages);

public record ChatResponse(string Reply);
