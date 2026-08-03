namespace AutonomousStore.ClientApp.Models;

public record RegisterRequest(string Name, string Email, string PhoneNumber, string Cpf, string Password);

public record LoginRequest(string Email, string Password);

public record GoogleLoginRequest(string IdToken);

public record CompleteGoogleRegistrationRequest(string Name, string Email, string PhoneNumber, string Cpf, string GoogleId);

public record AuthResponse(string Token, CustomerDto Customer);

public record GoogleNeedsCpfResponse(string Error, string Email, string Name, string GoogleId);

public record PendingGoogleSignup(string Email, string Name, string GoogleId);

public record ForgotPasswordRequest(string Email);

public record ResetPasswordRequest(string Email, string Token, string NewPassword);
