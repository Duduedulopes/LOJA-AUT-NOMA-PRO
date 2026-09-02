namespace AutonomousStore.SuporteApp.Models;

public record SuporteLoginRequest(string Email, string Password);

/// <summary>
/// Os mesmos campos do cadastro de cliente, menos o login pelo Google.
/// A ordem acompanha a do servidor de proposito: record posicional trocado
/// de ordem compila e grava o telefone no CPF.
/// </summary>
public record SuporteRegisterRequest(
    string Name,
    string Email,
    string PhoneNumber,
    string Cpf,
    string Password,
    string ConfirmPassword);

public record SuporteAuthResponse(string Token, Guid Id, string Name, string Email);
