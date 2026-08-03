using System.Security.Cryptography;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Auth;
using AutonomousStore.WebApi.Contracts.Customers;
using AutonomousStore.WebApi.Services;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ICustomerRepository _customerRepository;
    private readonly IJwtTokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IEmailService _emailService;
    private readonly PasswordHasher<Customer> _passwordHasher = new();

    public AuthController(
        ICustomerRepository customerRepository,
        IJwtTokenService tokenService,
        IConfiguration configuration,
        IEmailService emailService)
    {
        _customerRepository = customerRepository;
        _tokenService = tokenService;
        _configuration = configuration;
        _emailService = emailService;
    }

    /// <summary>Cadastro tradicional (nome, e-mail, CPF, telefone e senha).</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (await _customerRepository.GetByEmailAsync(request.Email, cancellationToken) is not null)
            return Conflict(new { error = "Já existe um cliente cadastrado com este e-mail." });

        if (await _customerRepository.GetByCpfAsync(Domain.Common.CpfValidation.Normalize(request.Cpf), cancellationToken) is not null)
            return Conflict(new { error = "Já existe um cliente cadastrado com este CPF." });

        if (!PasswordPolicy.IsValid(request.Password))
            return BadRequest(new { error = PasswordPolicy.Description });

        Customer customer;

        try
        {
            // O hash é gerado antes de criar a entidade, já que o PasswordHasher precisa de uma instância de Customer
            // só pra compor o hash (não guarda relação nenhuma com os dados dela).
            var passwordHash = _passwordHasher.HashPassword(null!, request.Password);
            customer = Customer.RegisterWithPassword(request.Name, request.Email, request.PhoneNumber, request.Cpf, passwordHash);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _customerRepository.SaveChangesAsync(cancellationToken);

        await _emailService.SendWelcomeEmailAsync(customer.Email, customer.Name, cancellationToken);

        var token = _tokenService.GenerateToken(customer);
        return Ok(new AuthResponse(token, ToResponse(customer)));
    }

    /// <summary>Login tradicional com e-mail e senha.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (customer is null || customer.PasswordHash is null)
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        var result = _passwordHasher.VerifyHashedPassword(customer, customer.PasswordHash, request.Password);

        if (result == PasswordVerificationResult.Failed)
            return Unauthorized(new { error = "E-mail ou senha inválidos." });

        var token = _tokenService.GenerateToken(customer);
        return Ok(new AuthResponse(token, ToResponse(customer)));
    }

    /// <summary>
    /// Login/cadastro via Google: recebe o id_token que o Google Identity Services devolve no navegador,
    /// valida a assinatura dele com o Google, e cria ou reaproveita o cadastro do cliente pelo e-mail.
    /// </summary>
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        GoogleJsonWebSignature.Payload payload;

        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _configuration["Authentication:Google:ClientId"] }
            };
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, settings);
        }
        catch (InvalidJwtException)
        {
            return Unauthorized(new { error = "Token do Google inválido." });
        }

        var existing = await _customerRepository.GetByGoogleIdAsync(payload.Subject, cancellationToken)
            ?? await _customerRepository.GetByEmailAsync(payload.Email, cancellationToken);

        if (existing is not null)
        {
            if (existing.GoogleId is null)
                existing.LinkGoogleAccount(payload.Subject);

            await _customerRepository.SaveChangesAsync(cancellationToken);

            var existingToken = _tokenService.GenerateToken(existing);
            return Ok(new AuthResponse(existingToken, ToResponse(existing)));
        }

        // Cliente novo via Google ainda precisa completar CPF depois (o Google não fornece isso).
        // Aqui criamos com um CPF temporário inválido de propósito? Não — em vez disso, retornamos
        // um sinal para o app pedir o CPF antes de finalizar o cadastro. Ver GoogleRegisterRequest.
        return Conflict(new { error = "NEEDS_CPF", email = payload.Email, name = payload.Name, googleId = payload.Subject });
    }

    /// <summary>Completa o cadastro de um login Google novo, que ainda precisa informar CPF e telefone.</summary>
    [HttpPost("google/complete")]
    public async Task<ActionResult<AuthResponse>> CompleteGoogleRegistration(
        CompleteGoogleRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        if (await _customerRepository.GetByEmailAsync(request.Email, cancellationToken) is not null)
            return Conflict(new { error = "Já existe um cliente cadastrado com este e-mail." });

        if (await _customerRepository.GetByCpfAsync(Domain.Common.CpfValidation.Normalize(request.Cpf), cancellationToken) is not null)
            return Conflict(new { error = "Já existe um cliente cadastrado com este CPF." });

        Customer customer;

        try
        {
            customer = Customer.RegisterWithGoogle(request.Name, request.Email, request.PhoneNumber, request.Cpf, request.GoogleId);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _customerRepository.AddAsync(customer, cancellationToken);
        await _customerRepository.SaveChangesAsync(cancellationToken);

        await _emailService.SendWelcomeEmailAsync(customer.Email, customer.Name, cancellationToken);

        var token = _tokenService.GenerateToken(customer);
        return Ok(new AuthResponse(token, ToResponse(customer)));
    }

    /// <summary>
    /// Pede a redefinição de senha por e-mail. A resposta é sempre a mesma, exista ou não o
    /// e-mail cadastrado — assim ninguém consegue usar essa rota pra descobrir quais e-mails têm conta.
    /// </summary>
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (customer is not null)
        {
            var token = GenerateResetToken();
            customer.SetPasswordResetToken(token, DateTime.UtcNow.AddMinutes(30));
            await _customerRepository.SaveChangesAsync(cancellationToken);

            var clientAppBaseUrl = _configuration["ClientApp:BaseUrl"] ?? "https://localhost:7280";
            var resetLink = $"{clientAppBaseUrl}/redefinir-senha?email={Uri.EscapeDataString(customer.Email)}&token={Uri.EscapeDataString(token)}";

            await _emailService.SendPasswordResetEmailAsync(customer.Email, customer.Name, resetLink, cancellationToken);
        }

        return Ok(new { message = "Se esse e-mail estiver cadastrado, enviamos um link pra redefinir a senha." });
    }

    /// <summary>Confirma a troca de senha usando o token recebido por e-mail.</summary>
    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (customer is null || !customer.IsPasswordResetTokenValid(request.Token))
            return BadRequest(new { error = "Link inválido ou expirado. Solicite uma nova redefinição de senha." });

        if (!PasswordPolicy.IsValid(request.NewPassword))
            return BadRequest(new { error = PasswordPolicy.Description });

        var newHash = _passwordHasher.HashPassword(customer, request.NewPassword);
        customer.SetPasswordHash(newHash);
        customer.ClearPasswordResetToken();

        await _customerRepository.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static string GenerateResetToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
    }

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Email,
        customer.PhoneNumber,
        customer.IsActive,
        customer.CreatedAt,
        customer.PaymentMethods
            .Select(p => new PaymentMethodResponse(p.Id, p.Type, p.Provider, p.LastFourDigits, p.IsDefault))
            .ToList(),
        customer.PasswordHash is not null);
}

public record CompleteGoogleRegistrationRequest(string Name, string Email, string PhoneNumber, string Cpf, string GoogleId);
