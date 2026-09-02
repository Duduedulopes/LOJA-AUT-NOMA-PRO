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
/// suporte atende VÁRIAS lojas, não é uma pessoa da loja, e o dono da loja
/// não pode criar um usuário de suporte pela tela dele.
/// </summary>
/// <remarks>
/// POR QUE O REGISTER ESTÁ ABERTO — E POR QUE ISTO É OK AGORA.
///
/// Num cenário de produção este endpoint seria interno, atrás de aprovação
/// da equipe que administra a plataforma. Enquanto o projeto está em
/// desenvolvimento, o register aberto serve para criar o PRIMEIRO usuário
/// sem precisar de SQL nem de credencial hardcoded. Fecharemos isto antes
/// de ir para produção — e o primeiro usuário não é hardcoded.
/// </remarks>
[ApiController]
[Route("api/suporte-auth")]
public class SuporteAuthController : ControllerBase
{
    private readonly ISuporteUserRepository _suporteRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly PasswordHasher<SuporteUser> _passwordHasher = new();

    public SuporteAuthController(ISuporteUserRepository suporteRepository, IJwtTokenService jwtTokenService)
    {
        _suporteRepository = suporteRepository;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>Cria um tecnico de suporte.</summary>
    /// <remarks>
    /// A PORTA SO FICA ABERTA ATE O PRIMEIRO ENTRAR.
    ///
    /// Este cadastro estava aberto para qualquer um, com um comentario
    /// dizendo que seria fechado antes de producao. Porta aberta com
    /// bilhete continua sendo porta aberta — e esta da acesso a ocorrencia
    /// de TODAS as lojas: tag de RFID lida na porta, registro de saida sem
    /// pagamento, pilha de excecao. Quem chegasse na API criava uma conta e
    /// lia tudo.
    ///
    /// A regra agora: enquanto NAO HOUVER nenhum tecnico, o cadastro e
    /// aberto — nao ha o que proteger, e e assim que o primeiro usuario
    /// nasce sem senha no codigo e sem SQL na mao. A partir do segundo, so
    /// quem ja e do suporte cria outro.
    ///
    /// Isso resolve o problema do "primeiro usuario" sem deixar divida: nao
    /// existe um dia futuro em que alguem precise lembrar de fechar isto.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult<SuporteAuthResponse>> Register(SuporteRegisterRequest request, CancellationToken cancellationToken)
    {
        var jaTemTecnico = await _suporteRepository.ExisteAlgumAsync(cancellationToken);
        if (jaTemTecnico && !User.IsInRole("Suporte"))
        {
            // Nao diz "ja existe tecnico": isso confirmaria, para quem esta
            // sondando, que a instalacao tem suporte configurado.
            return Unauthorized(new { error = "Só um técnico de suporte pode cadastrar outro." });
        }

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

        var token = _jwtTokenService.GenerateSuporteToken(suporte);

        return Ok(new SuporteAuthResponse(token, suporte.Id, suporte.Name, suporte.Email));
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

        var token = _jwtTokenService.GenerateSuporteToken(suporte);

        return Ok(new SuporteAuthResponse(token, suporte.Id, suporte.Name, suporte.Email));
    }
}
