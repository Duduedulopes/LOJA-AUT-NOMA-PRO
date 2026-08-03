using AutonomousStore.WebApi.Contracts.Customers;

namespace AutonomousStore.WebApi.Contracts.Auth;

public record RegisterRequest(string Name, string Email, string PhoneNumber, string Cpf, string Password);

public record LoginRequest(string Email, string Password);

public record GoogleLoginRequest(string IdToken);

public record AuthResponse(string Token, CustomerResponse Customer);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);
