namespace AutonomousStore.AdminApp.Models;

public record AdminLoginRequest(string Email, string Password);

public record AdminAuthResponse(string Token, Guid Id, string Name, string Email);
