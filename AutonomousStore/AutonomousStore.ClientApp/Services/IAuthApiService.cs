using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

public interface IAuthApiService
{
    Task<(bool Success, AuthResponse? Result, string? Error)> RegisterAsync(RegisterRequest request);
    Task<(bool Success, AuthResponse? Result, string? Error)> LoginAsync(string email, string password);

    /// <summary>
    /// Tenta logar com Google. Se o e-mail ainda não tem cadastro, retorna NeedsCpf = true
    /// com os dados que o Google devolveu, pra tela pedir o CPF antes de finalizar.
    /// </summary>
    Task<(bool Success, AuthResponse? Result, bool NeedsCpf, PendingGoogleSignup? Pending, string? Error)> GoogleLoginAsync(string idToken);

    Task<(bool Success, AuthResponse? Result, string? Error)> CompleteGoogleRegistrationAsync(CompleteGoogleRegistrationRequest request);

    /// <summary>Pede o e-mail de redefinição de senha. Sempre "dá certo" do ponto de vista da API (não revela se o e-mail existe).</summary>
    Task<(bool Success, string? Error)> ForgotPasswordAsync(string email);

    Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword);
}
