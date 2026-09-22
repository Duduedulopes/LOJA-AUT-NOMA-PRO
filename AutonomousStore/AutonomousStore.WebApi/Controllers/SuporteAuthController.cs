using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.SuporteAuth;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

/// <summary>
/// Autenticação do técnico de suporte. Separada do admin de propósito:
/// suporte atende VÁRIAS empresas, não é uma pessoa da loja, e o dono da loja
/// não pode criar um usuário de suporte pela tela dele.
/// </summary>
/// <remarks>
/// O TÉCNICO É UM SERVIÇO DO CRIADOR.
///
/// Ele vem com a assinatura, não pertence a nenhuma empresa e responde ao
/// Criador da plataforma. Por isso quem cadastra técnico é só o Criador
/// (<see cref="Papeis.Criador"/>).
///
/// Antes, este cadastro era aberto até existir o primeiro técnico — uma
/// conveniência de desenvolvimento, anotada como "vamos fechar antes de
/// produção". Foi fechada agora: o primeiro Criador nasce com o código de
/// instalação (<c>POST /api/criador-auth/register</c>) e ele cadastra a equipe.
/// </remarks>
[ApiController]
[Route("api/suporte-auth")]
public class SuporteAuthController : ControllerBase
{
    private readonly ISuporteUserRepository _suporteRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IAuditoria _auditoria;
    private readonly PasswordHasher<SuporteUser> _passwordHasher = new();

    public SuporteAuthController(
        ISuporteUserRepository suporteRepository,
        IJwtTokenService jwtTokenService,
        IAuditoria auditoria)
    {
        _suporteRepository = suporteRepository;
        _jwtTokenService = jwtTokenService;
        _auditoria = auditoria;
    }

    /// <summary>Cria um técnico de suporte. Só o Criador.</summary>
    /// <remarks>
    /// A resposta NÃO traz token: quem chamou foi o Criador, que já está logado, e
    /// o token do técnico novo não é dele. Entregá-lo permitiria agir como o
    /// técnico sem que o login dele aparecesse na auditoria.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<SuporteAuthResponse>> Register(SuporteRegisterRequest request, CancellationToken cancellationToken)
    {
        // Anonimo e nao-Criador recebem a mesma recusa: dizer "so o Criador" a quem
        // nao esta logado confirmaria, a quem esta sondando, quem manda aqui.
        if (!User.IsInRole(Papeis.Criador))
            return Unauthorized(new { error = "Só o Criador da plataforma pode cadastrar técnicos de suporte." });

        if (!PasswordPolicy.IsValid(request.Password))
            return BadRequest(new { error = PasswordPolicy.Description });

        // Conferido AQUI, e nao so na tela: a tela e uma das entradas, nao a
        // unica. Swagger, curl e o proximo app entram pelo mesmo endpoint.
        if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
            return BadRequest(new { error = "As senhas não são iguais." });

        var existing = await _suporteRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (existing is not null)
            return Conflict(new { error = "Já existe um técnico de suporte cadastrado com esse e-mail." });

        SuporteUser suporte;

        try
        {
            var passwordHash = _passwordHasher.HashPassword(null!, request.Password);
            suporte = new SuporteUser(request.Name, request.Email, request.PhoneNumber,
                                      request.Cpf, passwordHash);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _suporteRepository.AddAsync(suporte, cancellationToken);
        await _suporteRepository.SaveChangesAsync(cancellationToken);

        await _auditoria.RegistrarAsync(
            "suporte.cadastrado", nameof(SuporteUser), suporte.Id.ToString(),
            cancellationToken: cancellationToken);

        return Ok(new SuporteAuthResponse(string.Empty, suporte.Id, suporte.Name, suporte.Email));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<SuporteAuthResponse>> Login(SuporteLoginRequest request, CancellationToken cancellationToken)
    {
        var suporte = await _suporteRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (suporte is null || !suporte.IsActive)
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        var result = _passwordHasher.VerifyHashedPassword(suporte, suporte.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        // O login do tecnico e o primeiro rastro do que ele faz depois.
        await _auditoria.RegistrarComoAsync(
            suporte.Id, Papeis.Suporte, suporte.Name, "login.suporte", nameof(SuporteUser),
            recursoId: suporte.Id.ToString(), cancellationToken: cancellationToken);

        var token = _jwtTokenService.GenerateSuporteToken(suporte);

        return Ok(new SuporteAuthResponse(token, suporte.Id, suporte.Name, suporte.Email));
    }
}
