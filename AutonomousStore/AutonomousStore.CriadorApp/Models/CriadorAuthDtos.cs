namespace AutonomousStore.CriadorApp.Models;

public record CriadorLoginRequest(string Email, string Password);

public record CriadorAuthResponse(string Token, Guid Id, string Name, string Email);
