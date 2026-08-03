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
    private readonly IJwtTokenService _jwtTokenService;
    private readonly PasswordHasher<AdminUser> _passwordHasher = new();

    public AdminAuthController(IAdminUserRepository adminRepository, IJwtTokenService jwtTokenService)
    {
        _adminRepository = adminRepository;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Nota do protótipo: aberto pra qualquer um cadastrar um admin, sem convite/aprovação.
    /// Serve pra criar a primeira conta enquanto o sistema está em desenvolvimento — isso
    /// precisa ser travado (ex: só um admin existente pode criar outro) antes de ir pra produção.
    /// </summary>
    [HttpPost("register")]
    public async Task<ActionResult<AdminAuthResponse>> Register(AdminRegisterRequest request, CancellationToken cancellationToken)
    {
        if (!PasswordPolicy.IsValid(request.Password))
            return BadRequest(new { error = PasswordPolicy.Description });

        var existing = await _adminRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (existing is not null)
            return Conflict(new { error = "Já existe um admin cadastrado com esse e-mail." });

        AdminUser admin;

        try
        {
            var passwordHash = _passwordHasher.HashPassword(null!, request.Password);
            admin = new AdminUser(request.Name, request.Email, passwordHash);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _adminRepository.AddAsync(admin, cancellationToken);
        await _adminRepository.SaveChangesAsync(cancellationToken);

        var token = _jwtTokenService.GenerateAdminToken(admin);

        return Ok(new AdminAuthResponse(token, admin.Id, admin.Name, admin.Email));
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

        var token = _jwtTokenService.GenerateAdminToken(admin);

        return Ok(new AdminAuthResponse(token, admin.Id, admin.Name, admin.Email));
    }
}
