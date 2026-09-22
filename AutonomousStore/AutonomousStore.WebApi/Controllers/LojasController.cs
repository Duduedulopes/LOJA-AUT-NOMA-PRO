using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Lojas;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

/// <summary>
/// As lojas da empresa do Admin — SÓ LEITURA. Quem abre, renomeia, desativa e reativa uma loja é o Criador ou o técnico de
/// suporte (<see cref="PlatformController"/>, em nome da empresa): lojas são capacidade da assinatura, e mexer nelas é
/// negócio da plataforma, não autoatendimento do Admin. A empresa é a do TOKEN — este controller nunca recebe empresa por
/// parâmetro, e o filtro do banco já separa as lojas de uma empresa das de outra.
/// </summary>
[ApiController]
[Route("api/lojas")]
[Authorize(Roles = Papeis.Admin)]
public class LojasController : ControllerBase
{
    private readonly IStoreRepository _lojas;
    private readonly ITenantRepository _empresas;
    private readonly ITenantContext _tenant;

    public LojasController(IStoreRepository lojas, ITenantRepository empresas, ITenantContext tenant)
    {
        _lojas = lojas;
        _empresas = empresas;
        _tenant = tenant;
    }

    [HttpGet]
    public async Task<ActionResult<LojasResponse>> Listar(CancellationToken cancellationToken)
    {
        var empresa = await EmpresaDoTokenAsync(cancellationToken);
        if (empresa is null) return NotFound(new { error = "Empresa não encontrada." });

        var lojas = await _lojas.ListarAsync(cancellationToken);

        return Ok(new LojasResponse(lojas.Select(ToResponse).ToList(), lojas.Count(l => l.IsActive), empresa.LimiteDeLojas));
    }

    // ── apoio ────────────────────────────────────────────────────────────

    private async Task<Tenant?> EmpresaDoTokenAsync(CancellationToken cancellationToken)
        => _tenant.TenantId is { } id ? await _empresas.GetByIdAsync(id, cancellationToken) : null;

    internal static LojaResponse ToResponse(Store loja)
        => new(loja.Id, loja.Nome, loja.Segmento, loja.IsActive, loja.CreatedAt);
}
