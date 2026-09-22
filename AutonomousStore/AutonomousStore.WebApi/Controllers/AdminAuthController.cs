using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.AdminAuth;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

[ApiController]
[Route("api/admin-auth")]
[AllowAnonymous]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminUserRepository _adminRepository;
    private readonly ITenantRepository _empresas;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();

    public AdminAuthController(
        IAdminUserRepository adminRepository,
        ITenantRepository empresas,
        IJwtTokenService jwtTokenService)
    {
        _adminRepository = adminRepository;
        _empresas = empresas;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// FECHADO. Este endpoint era aberto: qualquer pessoa na internet se cadastrava
    /// como Admin e recebia um token com acesso a tudo. Com várias empresas,
    /// isso seria uma empresa qualquer criando administrador dentro de outra.
    ///
    /// Agora quem cria uma empresa e o seu primeiro Admin é o Criador da
    /// plataforma (<c>POST /api/platform/empresas</c>).
    /// </summary>
    [HttpPost("register")]
    public ActionResult<AdminAuthResponse> Register(AdminRegisterRequest request)
    {
        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            error = "O cadastro de administradores é feito pelo Criador da plataforma, junto com a empresa.",
        });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AdminAuthResponse>> Login(AdminLoginRequest request, CancellationToken cancellationToken)
    {
        var admin = await _adminRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (admin is null || !admin.IsActive)
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        var result = _passwordHasher.VerifyHashedPassword(admin, admin.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        // Depois da senha, e nao antes: quem erra a senha nao descobre que a empresa
        // esta suspensa. So quem provou ser o Admin recebe essa informacao.
        var empresa = admin.TenantId is { } id
            ? await _empresas.GetByIdAsync(id, cancellationToken)
            : null;

        if (empresa is null)
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        if (!empresa.EstaAtiva)
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "O acesso desta empresa está suspenso. Fale com o suporte.",
            });

        var token = _jwtTokenService.GenerateAdminToken(admin);

        return Ok(new AdminAuthResponse(token, admin.Id, admin.Name, admin.Email));
    }
}
