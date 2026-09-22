using System.Security.Cryptography;
using System.Text;
using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.CriadorAuth;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

/// <summary>
/// Entrada do dono da plataforma. Espelha o login do Admin e do Suporte, com
/// uma diferença que importa: o primeiro Criador só nasce com o código de
/// instalação, e os seguintes só por um Criador já logado.
/// </summary>
[ApiController]
[Route("api/criador-auth")]
public class CriadorAuthController : ControllerBase
{
    private readonly ICriadorUserRepository _criadores;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditoria _auditoria;
    private readonly IConfiguration _configuracao;
    private readonly PasswordHasher<CriadorUser> _passwordHasher = new();

    public CriadorAuthController(
        ICriadorUserRepository criadores,
        IJwtTokenService jwtTokenService,
        IAuditoria auditoria,
        IConfiguration configuracao)
    {
        _criadores = criadores;
        _jwtTokenService = jwtTokenService;
        _auditoria = auditoria;
        _configuracao = configuracao;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<CriadorAuthResponse>> Register(
        CriadorRegisterRequest request, CancellationToken cancellationToken)
    {
        var jaTemCriador = await _criadores.ExisteAlgumAsync(cancellationToken);

        if (jaTemCriador)
        {
            // Depois do primeiro, so um Criador cadastra outro.
            if (!User.IsInRole(Papeis.Criador))
                return Unauthorized(new { error = MensagemDeRecusa });
        }
        else if (!CodigoDeInstalacaoConfere(request.CodigoDeInstalacao))
        {
            // O primeiro Criador: sem o codigo do servidor, ninguem. A mensagem e a
            // mesma de "ja existe": recusar de forma diferente contaria a quem
            // esta sondando se a instalacao ja tem dono.
            return Unauthorized(new { error = MensagemDeRecusa });
        }

        if (!PasswordPolicy.IsValid(request.Password))
            return BadRequest(new { error = PasswordPolicy.Description });

        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return BadRequest(new { error = "As senhas não são iguais." });

        if (await _criadores.GetByEmailAsync(request.Email, cancellationToken) is not null)
            return Conflict(new { error = "Já existe um Criador cadastrado com esse e-mail." });

        CriadorUser criador;
        try
        {
            criador = new CriadorUser(request.Name, request.Email, _passwordHasher.HashPassword(null!, request.Password));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _criadores.AddAsync(criador, cancellationToken);
        await _criadores.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarComoAsync(
            criador.Id, Papeis.Criador, criador.Name, "criador.criado", nameof(CriadorUser),
            recursoId: criador.Id.ToString(), cancellationToken: cancellationToken);

        return Ok(new CriadorAuthResponse(
            _jwtTokenService.GenerateCriadorToken(criador), criador.Id, criador.Name, criador.Email));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<CriadorAuthResponse>> Login(
        CriadorLoginRequest request, CancellationToken cancellationToken)
    {
        var criador = await _criadores.GetByEmailAsync(request.Email, cancellationToken);

        if (criador is null || !criador.IsActive)
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        var resultado = _passwordHasher.VerifyHashedPassword(criador, criador.PasswordHash, request.Password);
        if (resultado == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        await _auditoria.RegistrarComoAsync(
            criador.Id, Papeis.Criador, criador.Name, "login.criador", nameof(CriadorUser),
            recursoId: criador.Id.ToString(), cancellationToken: cancellationToken);

        return Ok(new CriadorAuthResponse(
            _jwtTokenService.GenerateCriadorToken(criador), criador.Id, criador.Name, criador.Email));
    }

    private const string MensagemDeRecusa = "Só o Criador da plataforma pode cadastrar outro Criador.";

    /// <summary>
    /// Sem código configurado no servidor, o cadastro do primeiro Criador fica
    /// DESLIGADO — o padrão seguro. Comparação em tempo constante, para o tempo
    /// da resposta não revelar quantos caracteres do código estavam certos.
    /// </summary>
    private bool CodigoDeInstalacaoConfere(string? informado)
    {
        var esperado = _configuracao["Criador:CodigoDeInstalacao"];

        if (string.IsNullOrEmpty(esperado) || string.IsNullOrEmpty(informado))
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(esperado), Encoding.UTF8.GetBytes(informado));
    }
}
