using AutonomousStore.Domain.Enums;

namespace AutonomousStore.WebApi.Contracts.Platform;

public record EmpresaResponse(
    Guid Id,
    int Codigo,
    string Nome,
    string Slug,
    StatusDoTenant Status,
    int LimiteDeLojas,
    DateTime CriadaEm);

/// <param name="AdminSenha">
/// A senha inicial do primeiro Admin, definida por você ao cadastrar a empresa.
/// (Convite por e-mail, com a pessoa escolhendo a própria senha, fica para depois.)
/// </param>
public record CriarEmpresaRequest(
    string Nome,
    string Slug,
    int LimiteDeLojas,
    string AdminNome,
    string AdminEmail,
    string AdminSenha);

public record DefinirLimiteDeLojasRequest(int Limite);

public record AuditoriaResponse(
    Guid Id,
    DateTime QuandoUtc,
    Guid? AtorId,
    string AtorPapel,
    string AtorNome,
    Guid? TenantAlvoId,
    string Acao,
    string Recurso,
    string? RecursoId,
    string? Motivo,
    string? Ip);
