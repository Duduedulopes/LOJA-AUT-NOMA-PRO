namespace AutonomousStore.WebApi.Contracts.CriadorAuth;

/// <param name="CodigoDeInstalacao">
/// Só exigido para criar o PRIMEIRO Criador de uma instalação. É um valor que só
/// quem instalou o sistema conhece (fica na configuração do servidor) — sem ele,
/// quem descobrisse a URL antes de você viraria o dono da plataforma.
/// </param>
public record CriadorRegisterRequest(
    string Name,
    string Email,
    string Password,
    string ConfirmPassword,
    string? CodigoDeInstalacao);

public record CriadorLoginRequest(string Email, string Password);

public record CriadorAuthResponse(string Token, Guid Id, string Name, string Email);
