namespace AutonomousStore.CriadorApp.Models;

public record TecnicoDto(Guid Id, string Nome, string Email, bool Ativo, DateTime CriadoEm);

/// <summary>
/// Os mesmos campos do cadastro de técnico no SuporteApp — a ordem acompanha a do servidor de propósito: um record
/// posicional trocado de ordem compila e grava o telefone no CPF.
/// </summary>
public record CadastrarTecnicoRequest(
    string Name,
    string Email,
    string PhoneNumber,
    string Cpf,
    string Password,
    string ConfirmPassword);
