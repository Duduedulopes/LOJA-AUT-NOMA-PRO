using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Lojas;
using AutonomousStore.WebApi.Contracts.Platform;
using AutonomousStore.WebApi.Services;
using AutonomousStore.WebApi.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

/// <summary>
/// A área do dono da plataforma: cadastrar empresas, cortar o acesso de quem
/// parou de pagar, ver o que foi feito. Só o Criador entra — e é a ÚNICA porta
/// do sistema que opera sobre todas as empresas de uma vez.
///
/// Toda ação daqui grava na auditoria. O Criador não é exceção à regra de que
/// "tudo que mexe em empresa deixa rastro": é quem mais precisa dela.
/// </summary>
[ApiController]
[Route("api/platform")]
[Authorize(Roles = Papeis.Criador)]
public class PlatformController : ControllerBase
{
    private readonly ITenantRepository _empresas;
    private readonly IStoreRepository _lojas;
    private readonly ICustomerRepository _compradores;
    private readonly ISuporteUserRepository _tecnicos;
    private readonly IOcorrenciaRepository _ocorrencias;
    private readonly IAdminUserRepository _admins;
    private readonly IAuditoriaRepository _auditorias;
    private readonly IAuditoria _auditoria;
    private readonly TenantResolver _resolvedor;
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();

    public PlatformController(
        ITenantRepository empresas,
        IStoreRepository lojas,
        ICustomerRepository compradores,
        ISuporteUserRepository tecnicos,
        IOcorrenciaRepository ocorrencias,
        IAdminUserRepository admins,
        IAuditoriaRepository auditorias,
        IAuditoria auditoria,
        TenantResolver resolvedor)
    {
        _empresas = empresas;
        _lojas = lojas;
        _compradores = compradores;
        _tecnicos = tecnicos;
        _ocorrencias = ocorrencias;
        _admins = admins;
        _auditorias = auditorias;
        _auditoria = auditoria;
        _resolvedor = resolvedor;
    }

    [HttpGet("empresas")]
    public async Task<ActionResult<IReadOnlyList<EmpresaResponse>>> Listar(CancellationToken cancellationToken)
    {
        var empresas = await _empresas.GetAllAsync(cancellationToken);
        return Ok(empresas.Select(ToResponse).ToList());
    }

    [HttpPost("empresas")]
    public async Task<ActionResult<EmpresaResponse>> Criar(
        CriarEmpresaRequest request, CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsValid(request.AdminSenha))
            return BadRequest(new { error = PasswordPolicy.Description });

        Tenant empresa;
        AdminUser admin;
        try
        {
            empresa = new Tenant(request.Nome, request.Slug, request.LimiteDeLojas);

            admin = new AdminUser(
                request.AdminNome, request.AdminEmail,
                _passwordHasher.HashPassword(null!, request.AdminSenha));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        if (await _empresas.GetBySlugAsync(empresa.Slug, cancellationToken) is not null)
            return Conflict(new { error = "Já existe uma empresa com esse identificador." });

        // O e-mail do Admin e unico na plataforma inteira: e ele que, no login,
        // revela a qual empresa a pessoa pertence.
        if (await _admins.GetByEmailAsync(request.AdminEmail, cancellationToken) is not null)
            return Conflict(new { error = "Já existe um administrador cadastrado com esse e-mail." });

        // O Criador nao tem empresa, entao o carimbo automatico nao sabe qual pôr:
        // aqui ele diz, explicitamente, em nome de qual empresa esta gravando.
        admin.PertenceAoTenant(empresa.Id);

        // Empresa e primeiro Admin no mesmo salvamento: ou nascem os dois, ou nenhum.
        // Uma empresa sem ninguem que consiga entrar nela seria um beco sem saida.
        await _empresas.AddAsync(empresa, cancellationToken);
        await _admins.AddAsync(admin, cancellationToken);
        await _empresas.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "empresa.criada", nameof(Tenant), empresa.Id.ToString(), empresa.Id,
            cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(Listar), new { id = empresa.Id }, ToResponse(empresa));
    }

    [HttpPost("empresas/{id:guid}/suspender")]
    public Task<ActionResult<EmpresaResponse>> Suspender(Guid id, CancellationToken cancellationToken)
        => Alterar(id, "empresa.suspensa", e => e.Suspender(), motivo: null, cancellationToken);

    [HttpPost("empresas/{id:guid}/reativar")]
    public Task<ActionResult<EmpresaResponse>> Reativar(Guid id, CancellationToken cancellationToken)
        => Alterar(id, "empresa.reativada", e => e.Reativar(), motivo: null, cancellationToken);

    [HttpPut("empresas/{id:guid}/limite-de-lojas")]
    public async Task<ActionResult<EmpresaResponse>> DefinirLimite(
        Guid id, DefinirLimiteDeLojasRequest request, CancellationToken cancellationToken)
    {
        var empresa = await _empresas.GetByIdAsync(id, cancellationToken);
        if (empresa is null)
            return NotFound(new { error = "Empresa não encontrada." });

        // Baixar o limite abaixo do que a empresa JA usa deixaria a assinatura num estado que nao fecha: 3 lojas ativas num
        // plano de 2. Quem reduz desativa antes — qual loja sai e decisao do cliente, e nao do sistema.
        var ativas = await _lojas.ContarAtivasAsync(id, cancellationToken);
        if (request.Limite < ativas)
            return Conflict(new
            {
                error = $"A empresa tem {ativas} loja(s) ativa(s). Desative-as antes de reduzir o limite para {request.Limite}.",
            });

        return await Alterar(
            id, "empresa.limite-de-lojas", e => e.DefinirLimiteDeLojas(request.Limite),
            motivo: $"de {empresa.LimiteDeLojas} para {request.Limite}", cancellationToken);
    }

    /// <summary>As lojas de uma empresa, e quanto do limite ela usa. O Criador olha; quem cria e desativa e o Admin da empresa.</summary>
    [HttpGet("empresas/{id:guid}/lojas")]
    public async Task<ActionResult<LojasResponse>> LojasDaEmpresa(Guid id, CancellationToken cancellationToken)
    {
        var empresa = await _empresas.GetByIdAsync(id, cancellationToken);
        if (empresa is null)
            return NotFound(new { error = "Empresa não encontrada." });

        var lojas = await _lojas.ListarDaEmpresaAsync(id, cancellationToken);

        return Ok(new LojasResponse(
            lojas.Select(LojasController.ToResponse).ToList(), lojas.Count(l => l.IsActive), empresa.LimiteDeLojas));
    }

    /// <summary>
    /// A tela inicial do Criador: empresas, lojas contra o limite, equipe, compradores, o suporte (fila e desempenho dos últimos
    /// 30 dias) e uma linha por empresa. Uma chamada só, para a tela não fazer uma por número.
    /// </summary>
    [HttpGet("painel")]
    public async Task<ActionResult<PainelResponse>> Painel(CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;

        var empresas = await _empresas.GetAllAsync(cancellationToken);
        var lojas = await _lojas.ListarAsync(cancellationToken);
        var compradores = await _compradores.ContarPorEmpresaAsync(cancellationToken);
        var tecnicos = await _tecnicos.ListarAsync(cancellationToken);
        var naFila = await _ocorrencias.NaFilaDoSuporteAsync(cancellationToken);
        var resolvidas = await _ocorrencias.ResolvidasDesdeAsync(
            agora.AddDays(-PainelDaPlataforma.DiasDoDesempenho), cancellationToken);

        return Ok(PainelDaPlataforma.Montar(
            empresas, lojas, compradores, tecnicos.Count(t => t.IsActive), naFila, resolvidas, agora));
    }

    /// <summary>A equipe de suporte. Cadastrar é em <c>POST /api/suporte-auth/register</c>, também só do Criador.</summary>
    [HttpGet("tecnicos")]
    public async Task<ActionResult<IReadOnlyList<TecnicoResponse>>> Tecnicos(CancellationToken cancellationToken)
    {
        var equipe = await _tecnicos.ListarAsync(cancellationToken);

        return Ok(equipe.Select(t => new TecnicoResponse(t.Id, t.Name, t.Email, t.IsActive, t.CreatedAt)).ToList());
    }

    [HttpGet("auditoria")]
    public async Task<ActionResult<IReadOnlyList<AuditoriaResponse>>> Auditoria(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? ate,
        [FromQuery] Guid? empresaId,
        [FromQuery] Guid? atorId,
        [FromQuery] string? acao,
        [FromQuery] int limite = 200,
        CancellationToken cancellationToken = default)
    {
        var registros = await _auditorias.BuscarAsync(
            new FiltroDeAuditoria(desde, ate, empresaId, atorId, acao, limite), cancellationToken);

        return Ok(registros.Select(a => new AuditoriaResponse(
            a.Id, a.QuandoUtc, a.AtorId, a.AtorPapel, a.AtorNome, a.TenantAlvoId,
            a.Acao, a.Recurso, a.RecursoId, a.Motivo, a.Ip)).ToList());
    }

    private async Task<ActionResult<EmpresaResponse>> Alterar(
        Guid id, string acao, Action<Tenant> mudanca, string? motivo, CancellationToken cancellationToken)
    {
        var empresa = await _empresas.GetByIdAsync(id, cancellationToken);
        if (empresa is null)
            return NotFound(new { error = "Empresa não encontrada." });

        try
        {
            mudanca(empresa);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _empresas.SaveChangesAsync(cancellationToken);

        // Sem isto a suspensao so valeria quando o cache vencesse (30 segundos).
        _resolvedor.Esquecer(empresa);

        await _auditoria.RegistrarAsync(
            acao, nameof(Tenant), empresa.Id.ToString(), empresa.Id, motivo, cancellationToken);

        return Ok(ToResponse(empresa));
    }

    private static EmpresaResponse ToResponse(Tenant e)
        => new(e.Id, e.Codigo, e.Nome, e.Slug, e.Status, e.LimiteDeLojas, e.CreatedAt);
}
