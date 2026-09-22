using System.Security.Cryptography;
using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Customers;
using AutonomousStore.WebApi.Contracts.Suporte;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.WebApi.Controllers;

/// <summary>
/// O que o técnico de suporte vê e faz sobre as empresas e os compradores delas.
///
/// O técnico atende TODAS as empresas, mas não é dono de nenhuma. Por isso aqui:
/// a empresa aparece só como código + nome fantasia; a pessoa aparece com o dado
/// pessoal MASCARADO; e ver o dado completo exige um motivo que fica na auditoria.
///
/// Uma rota separada, e não o <c>CustomersController</c> com um "se for técnico
/// mascara": quando a regra de privacidade mora numa rota só dele, um endpoint novo
/// escrito amanhã não herda por acidente uma resposta com o CPF inteiro.
/// </summary>
[ApiController]
[Route("api/suporte")]
[Authorize(Roles = Papeis.Suporte)]
public class SuporteController : ControllerBase
{
    private const int TamanhoMinimoDoMotivo = 10;

    private readonly ITenantRepository _empresas;
    private readonly ICustomerRepository _compradores;
    private readonly IAuditoria _auditoria;
    private readonly PasswordHasher<Customer> _passwordHasher = new();

    public SuporteController(
        ITenantRepository empresas,
        ICustomerRepository compradores,
        IAuditoria auditoria)
    {
        _empresas = empresas;
        _compradores = compradores;
        _auditoria = auditoria;
    }

    [HttpGet("empresas")]
    public async Task<ActionResult<IReadOnlyList<EmpresaParaSuporteResponse>>> Empresas(
        CancellationToken cancellationToken)
    {
        var empresas = await _empresas.GetAllAsync(cancellationToken);

        return Ok(empresas
            .OrderBy(e => e.Codigo)
            .Select(e => new EmpresaParaSuporteResponse(e.Id, e.Codigo, e.Rotulo, e.EstaAtiva))
            .ToList());
    }

    [HttpGet("compradores")]
    public async Task<ActionResult<IReadOnlyList<CompradorParaSuporteResponse>>> Compradores(
        [FromQuery] Guid? empresaId,
        [FromQuery] string? busca,
        [FromQuery] int limite = 50,
        CancellationToken cancellationToken = default)
    {
        var lista = await _compradores.ListarAsync(empresaId, busca, limite, cancellationToken);
        var rotulos = await RotulosAsync(lista.Select(c => c.TenantId), cancellationToken);

        return Ok(lista.Select(c => Mascarado(c, rotulos)).ToList());
    }

    [HttpGet("compradores/{id:guid}")]
    public async Task<ActionResult<CompradorParaSuporteResponse>> Comprador(
        Guid id, CancellationToken cancellationToken)
    {
        var comprador = await _compradores.GetByIdAsync(id, cancellationToken);
        if (comprador is null)
            return NotFound(new { error = "Comprador não encontrado." });

        var rotulos = await RotulosAsync([comprador.TenantId], cancellationToken);

        return Ok(Mascarado(comprador, rotulos));
    }

    /// <summary>
    /// Mostra o dado completo de UM comprador. Exige um motivo, e o motivo fica na
    /// auditoria com quem pediu, quando, e de qual empresa era o comprador.
    /// </summary>
    [HttpPost("compradores/{id:guid}/revelar")]
    public async Task<ActionResult<CompradorReveladoResponse>> Revelar(
        Guid id, RevelarRequest request, CancellationToken cancellationToken)
    {
        var motivo = request.Motivo?.Trim() ?? "";

        // Um motivo de uma letra tornaria a auditoria decorativa: ela existe para
        // que, depois, alguem possa perguntar "por que voce viu isso?".
        if (motivo.Length < TamanhoMinimoDoMotivo)
            return BadRequest(new { error = $"Explique o motivo (pelo menos {TamanhoMinimoDoMotivo} caracteres)." });

        var comprador = await _compradores.GetByIdAsync(id, cancellationToken);
        if (comprador is null)
            return NotFound(new { error = "Comprador não encontrado." });

        // ANTES de devolver o dado: se a auditoria falhar, o dado nao sai.
        await _auditoria.RegistrarAsync(
            "comprador.revelado", nameof(Customer), comprador.Id.ToString(),
            comprador.TenantId, motivo, cancellationToken);

        return Ok(new CompradorReveladoResponse(
            comprador.Id, comprador.Name, comprador.Email, comprador.Cpf, comprador.PhoneNumber));
    }

    /// <summary>
    /// Cadastra um comprador em uma empresa. O técnico não escolhe a senha e nem a
    /// conhece: nasce uma aleatória, e a pessoa define a dela por "esqueci minha senha".
    /// </summary>
    [HttpPost("compradores")]
    public async Task<ActionResult<CompradorParaSuporteResponse>> CriarComprador(
        CriarCompradorRequest request, CancellationToken cancellationToken)
    {
        var empresa = await _empresas.GetByIdAsync(request.EmpresaId, cancellationToken);
        if (empresa is null)
            return NotFound(new { error = "Empresa não encontrada." });

        if (!empresa.EstaAtiva)
            return Conflict(new { error = "Esta empresa está suspensa; não dá para cadastrar compradores nela." });

        Customer comprador;
        try
        {
            comprador = Customer.RegisterWithPassword(
                request.Nome, request.Email, request.Telefone ?? "", request.Cpf, SenhaQueNinguemConhece());
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        // A pergunta nomeia a empresa: para quem ve todas, "existe alguem com este
        // e-mail?" acharia a mesma pessoa em OUTRA empresa e recusaria um cadastro
        // perfeitamente valido (a mesma pessoa pode ter um cadastro em cada uma).
        if (await _compradores.ExisteNaEmpresaAsync(empresa.Id, comprador.Email, comprador.Cpf, cancellationToken))
            return Conflict(new { error = "Já existe um comprador com esse e-mail ou CPF nesta empresa." });

        // O tecnico nao tem empresa; ele diz, explicitamente, em nome de qual esta gravando.
        comprador.PertenceAoTenant(empresa.Id);

        await _compradores.AddAsync(comprador, cancellationToken);

        try
        {
            await _compradores.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Duas pessoas cadastrando o mesmo comprador no mesmo instante: a checagem
            // acima nao viu, mas o indice unico do banco viu.
            return Conflict(new { error = "Já existe um comprador com esse e-mail ou CPF nesta empresa." });
        }

        await _auditoria.RegistrarAsync(
            "comprador.cadastrado", nameof(Customer), comprador.Id.ToString(), empresa.Id,
            cancellationToken: cancellationToken);

        return CreatedAtAction(
            nameof(Comprador), new { id = comprador.Id },
            Mascarado(comprador, new Dictionary<Guid, string> { [empresa.Id] = empresa.Rotulo }));
    }

    // ── tradução ─────────────────────────────────────────────────────────

    private async Task<IReadOnlyDictionary<Guid, string>> RotulosAsync(
        IEnumerable<Guid?> empresaIds, CancellationToken cancellationToken)
    {
        var ids = empresaIds.OfType<Guid>().Distinct().ToList();
        var empresas = await _empresas.GetByIdsAsync(ids, cancellationToken);

        return empresas.ToDictionary(e => e.Id, e => e.Rotulo);
    }

    private static CompradorParaSuporteResponse Mascarado(
        Customer c, IReadOnlyDictionary<Guid, string> rotulos) => new(
            c.Id,
            c.TenantId,
            c.TenantId is { } t && rotulos.TryGetValue(t, out var rotulo) ? rotulo : null,
            c.Name,
            Mascara.Email(c.Email),
            Mascara.Cpf(c.Cpf),
            Mascara.Telefone(c.PhoneNumber),
            c.IsActive,
            c.CreatedAt,
            c.PasswordHash is not null,
            c.PaymentMethods
                .Select(p => new PaymentMethodResponse(p.Id, p.Type, p.Provider, p.LastFourDigits, p.IsDefault))
                .ToList());

    /// <summary>
    /// 32 bytes aleatórios que nunca aparecem em lugar nenhum: o hash é gravado e a senha
    /// em si é descartada. É só para o cadastro ter uma senha; quem entra usa "esqueci
    /// minha senha".
    /// </summary>
    private string SenhaQueNinguemConhece()
    {
        var aleatoria = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return _passwordHasher.HashPassword(null!, aleatoria);
    }
}
