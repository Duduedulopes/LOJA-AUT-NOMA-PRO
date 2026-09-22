using System.Security.Claims;
using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;

namespace AutonomousStore.WebApi.Services;

public interface IAuditoria
{
    /// <summary>Registra uma ação de quem está logado na requisição atual.</summary>
    Task RegistrarAsync(
        string acao, string recurso, string? recursoId = null, Guid? tenantAlvoId = null,
        string? motivo = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registra uma ação em nome de alguém que ainda não tem token na requisição —
    /// o próprio login, por exemplo, que é o que gera o token.
    /// </summary>
    Task RegistrarComoAsync(
        Guid atorId, string papel, string nome, string acao, string recurso,
        string? recursoId = null, Guid? tenantAlvoId = null, string? motivo = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Não engole erro, de propósito. Uma auditoria que falha em silêncio é pior que
/// nenhuma: dá a impressão de que tudo o que o suporte fez está registrado.
/// Se ela não grava, a ação que dependia dela também falha — e as duas moram no
/// mesmo banco, então uma coisa não cai sem a outra.
/// </summary>
public class AuditoriaService : IAuditoria
{
    // Do mais poderoso para o menos: quem tiver dois papéis e registrado pelo maior.
    private static readonly string[] PapeisEmOrdem =
        [Papeis.Criador, Papeis.Suporte, Papeis.Admin, Papeis.Comprador];

    private readonly IAuditoriaRepository _repositorio;
    private readonly IHttpContextAccessor _http;

    public AuditoriaService(IAuditoriaRepository repositorio, IHttpContextAccessor http)
    {
        _repositorio = repositorio;
        _http = http;
    }

    public async Task RegistrarAsync(
        string acao, string recurso, string? recursoId = null, Guid? tenantAlvoId = null,
        string? motivo = null, CancellationToken cancellationToken = default)
    {
        var usuario = _http.HttpContext?.User;

        var papel = PapeisEmOrdem.FirstOrDefault(p => usuario?.IsInRole(p) == true) ?? "Anonimo";
        var id = usuario?.FindFirstValue(ClaimTypes.NameIdentifier) ?? usuario?.FindFirstValue("sub");
        var nome = usuario?.FindFirstValue(ClaimTypes.Name) ?? "—";

        await GravarAsync(
            Guid.TryParse(id, out var atorId) ? atorId : null,
            papel, nome, acao, recurso, recursoId, tenantAlvoId, motivo, cancellationToken);
    }

    public Task RegistrarComoAsync(
        Guid atorId, string papel, string nome, string acao, string recurso,
        string? recursoId = null, Guid? tenantAlvoId = null, string? motivo = null,
        CancellationToken cancellationToken = default)
        => GravarAsync(atorId, papel, nome, acao, recurso, recursoId, tenantAlvoId, motivo, cancellationToken);

    private async Task GravarAsync(
        Guid? atorId, string papel, string nome, string acao, string recurso,
        string? recursoId, Guid? tenantAlvoId, string? motivo, CancellationToken cancellationToken)
    {
        var ip = _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

        await _repositorio.AddAsync(
            new Auditoria(DateTime.UtcNow, atorId, papel, nome, acao, recurso, tenantAlvoId, recursoId, motivo, ip),
            cancellationToken);

        await _repositorio.SaveChangesAsync(cancellationToken);
    }
}
