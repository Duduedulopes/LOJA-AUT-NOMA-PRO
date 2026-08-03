namespace AutonomousStore.WebApi.Contracts.Vision;

public record DetectShelfChangeRequest(List<Guid> ProductIds, string BeforeImageBase64, string AfterImageBase64);

public record DetectShelfChangeResponse(string Action, string? ProductName, string Message);
