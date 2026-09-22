namespace AutonomousStore.CriadorApp.Models;

public record AuditoriaDto(
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
