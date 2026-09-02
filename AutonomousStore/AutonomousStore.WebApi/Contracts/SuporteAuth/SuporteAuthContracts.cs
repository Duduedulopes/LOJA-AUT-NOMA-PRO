namespace AutonomousStore.WebApi.Contracts.SuporteAuth;

/// <summary>Cadastro de técnico de suporte.</summary>
/// <remarks>
/// OS MESMOS CAMPOS DO CLIENTE, menos o login pelo Google — que é conveniência
/// de consumidor e não de equipe.
///
/// `ConfirmPassword` vem no contrato, e não só na tela: a tela pode ser
/// contornada (Swagger, curl, um app novo amanhã), e a confirmação existe
/// justamente porque um erro de digitação numa senha que ninguém vê tranca a
/// pessoa para fora de um sistema que só ela administra.
/// </remarks>
public record SuporteRegisterRequest(
    string Name,
    string Email,
    string PhoneNumber,
    string Cpf,
    string Password,
    string ConfirmPassword);

public record SuporteLoginRequest(string Email, string Password);

public record SuporteAuthResponse(string Token, Guid Id, string Name, string Email);
