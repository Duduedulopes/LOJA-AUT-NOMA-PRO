namespace AutonomousStore.WebApi.Contracts.AdminAuth;

public record AdminRegisterRequest(string Name, string Email, string Password);

public record AdminLoginRequest(string Email, string Password);

public record AdminAuthResponse(string Token, Guid Id, string Name, string Email);
