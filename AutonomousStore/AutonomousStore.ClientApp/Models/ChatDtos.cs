namespace AutonomousStore.ClientApp.Models;

public record ChatMessageDto(string Role, string Text);

public record ChatRequest(List<ChatMessageDto> Messages);

public record ChatResponse(string Reply);
