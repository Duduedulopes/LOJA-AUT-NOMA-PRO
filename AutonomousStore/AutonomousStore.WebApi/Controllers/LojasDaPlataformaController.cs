using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Lojas;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

/// <summary>
/// Quem abre, renomeia, desativa e reativa a loja de uma empresa: o Criador ou o técnico de suporte, nunca o Admin da
/// própria empresa — lojas são capacidade da assinatura, negócio da plataforma. O <see cref="LojasController"/> continua
/// existindo só para o Admin CONSULTAR as lojas dele; este é o único que grava.
///
/// UM CONTROLLER À PARTE, e não dentro do <see cref="PlatformController"/>: a classe inteira dali é
/// <c>[Authorize(Roles = Papeis.Criador)]</c>, e em ASP.NET Core um <c>[Authorize]</c> no método SOMA ao da classe — não o
/// afrouxa. Não haveria como abrir só estas quatro rotas para o Suporte sem separar a classe.
/// </summary>
[ApiController]
[Route("api/platform/empresas/{empresaId:guid}/lojas")]
[Authorize(Roles = Papeis.DaPlataforma)]
public class LojasDaPlataformaController : ControllerBase
{
    private readonly IStoreRepository _lojas;
    private readonly ITenantRepository _empresas;
    private readonly IAuditoria _auditoria;

    public LojasDaPlataformaController(IStoreRepository lojas, ITenantRepository empresas, IAuditoria auditoria)
    {
        _lojas = lojas;
        _empresas = empresas;
        _auditoria = auditoria;
    }

    [HttpPost]
    public async Task<ActionResult<LojaResponse>> Criar(
        Guid empresaId, SalvarLojaRequest request, CancellationToken cancellationToken)
    {
        var empresa = await _empresas.GetByIdAsync(empresaId, cancellationToken);
        if (empresa is null) return NotFound(new { error = "Empresa não encontrada." });
        if (!empresa.EstaAtiva) return Suspensa();

        Store loja;
        try
        {
            loja = new Store(request.Nome, request.Segmento);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        if (await _lojas.ExisteComNomeAsync(empresa.Id, loja.Nome, exceto: null, cancellationToken))
            return Conflict(new { error = "Já existe uma loja com esse nome." });

        // O LIMITE: o que o preco da assinatura acompanha. Conferir e gravar sao dois passos — limite de plano, nao de
        // seguranca, e aceito de proposito (ver StoreTests/StoreRepositoryTests).
        var ativas = await _lojas.ContarAtivasAsync(empresa.Id, cancellationToken);
        if (!empresa.PodeTerMaisUmaLoja(ativas))
            return Conflict(new { error = MensagemDeLimite(empresa, ativas) });

        loja.PertenceAoTenant(empresa.Id);     // quem grava e o Criador/Suporte, sem TenantId no token: o carimbo automatico nao sabe qual empresa

        await _lojas.AddAsync(loja, cancellationToken);
        await _lojas.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "loja.criada", nameof(Store), loja.Id.ToString(), empresa.Id, cancellationToken: cancellationToken);

        // Aponta para a MESMA lista que PlatformController.LojasDaEmpresa devolve — o mesmo padrao que LojasController.Criar
        // usa (aponta para "Listar", e nao para si mesmo): nao ha rota GET de uma loja isolada, so da lista da empresa.
        var local = Url.Action(nameof(PlatformController.LojasDaEmpresa), "Platform", new { id = empresaId });
        return Created(local ?? string.Empty, LojasController.ToResponse(loja));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LojaResponse>> Atualizar(
        Guid empresaId, Guid id, SalvarLojaRequest request, CancellationToken cancellationToken)
    {
        var loja = await LojaDaEmpresaAsync(empresaId, id, cancellationToken);
        if (loja is null) return NotFound(new { error = "Loja não encontrada." });

        try
        {
            loja.AtualizarDados(request.Nome, request.Segmento);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        if (await _lojas.ExisteComNomeAsync(empresaId, loja.Nome, exceto: loja.Id, cancellationToken))
            return Conflict(new { error = "Já existe uma loja com esse nome." });

        await _lojas.SaveChangesAsync(cancellationToken);

        return Ok(LojasController.ToResponse(loja));
    }

    [HttpPost("{id:guid}/desativar")]
    public async Task<ActionResult<LojaResponse>> Desativar(Guid empresaId, Guid id, CancellationToken cancellationToken)
    {
        var loja = await LojaDaEmpresaAsync(empresaId, id, cancellationToken);
        if (loja is null) return NotFound(new { error = "Loja não encontrada." });

        if (loja.IsActive)
        {
            loja.Deactivate();
            await _lojas.SaveChangesAsync(cancellationToken);

            await _auditoria.RegistrarAsync(
                "loja.desativada", nameof(Store), loja.Id.ToString(), empresaId, cancellationToken: cancellationToken);
        }

        return Ok(LojasController.ToResponse(loja));
    }

    [HttpPost("{id:guid}/ativar")]
    public async Task<ActionResult<LojaResponse>> Ativar(Guid empresaId, Guid id, CancellationToken cancellationToken)
    {
        var empresa = await _empresas.GetByIdAsync(empresaId, cancellationToken);
        var loja = await LojaDaEmpresaAsync(empresaId, id, cancellationToken);
        if (empresa is null || loja is null) return NotFound(new { error = "Loja não encontrada." });

        if (!loja.IsActive)
        {
            if (!empresa.EstaAtiva) return Suspensa();

            // Reativar AUMENTA o numero de lojas ativas: passa pela mesma checagem de criar. Sem isto, desativar, criar e
            // reativar burlaria o limite.
            var ativas = await _lojas.ContarAtivasAsync(empresa.Id, cancellationToken);
            if (!empresa.PodeTerMaisUmaLoja(ativas))
                return Conflict(new { error = MensagemDeLimite(empresa, ativas) });

            loja.Activate();
            await _lojas.SaveChangesAsync(cancellationToken);

            await _auditoria.RegistrarAsync(
                "loja.reativada", nameof(Store), loja.Id.ToString(), empresa.Id, cancellationToken: cancellationToken);
        }

        return Ok(LojasController.ToResponse(loja));
    }

    // ── apoio ────────────────────────────────────────────────────────────

    /// <summary>A loja, só se ela pertencer MESMO à empresa da URL — sem isto, quem sabe o id de uma loja de outra empresa
    /// poderia mexer nela chamando com o {empresaId} errado.</summary>
    private async Task<Store?> LojaDaEmpresaAsync(Guid empresaId, Guid id, CancellationToken cancellationToken)
    {
        var loja = await _lojas.GetByIdAsync(id, cancellationToken);
        return loja is null || loja.TenantId != empresaId ? null : loja;
    }

    private static string MensagemDeLimite(Tenant empresa, int ativas)
        => $"A assinatura permite {empresa.LimiteDeLojas} loja(s) ativa(s), e já há {ativas}. "
         + "Desative uma loja para abrir vaga, ou amplie o limite do plano.";

    // Uma empresa suspensa nao ganha capacidade nova: abrir ou reativar loja aumentaria o que ela usa exatamente
    // enquanto nao esta pagando. Renomear e desativar continuam liberados (nao aumentam nada).
    private ObjectResult Suspensa()
        => StatusCode(StatusCodes.Status403Forbidden, new
        {
            error = "Esta empresa está com o acesso suspenso. Reative-a antes de abrir ou reativar uma loja.",
        });
}
