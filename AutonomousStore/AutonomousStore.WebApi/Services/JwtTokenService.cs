using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AutonomousStore.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace AutonomousStore.WebApi.Services;

public interface IJwtTokenService
{
    string GenerateToken(Customer customer);
    string GenerateAdminToken(AdminUser admin);
    string GenerateSuporteToken(SuporteUser suporte);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string GenerateToken(Customer customer)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, customer.Email),
            new Claim(ClaimTypes.Name, customer.Name),
        };

        return BuildToken(claims);
    }

    /// <summary>
    /// Token de admin: igual ao de cliente, mas com uma claim de role "Admin" a mais —
    /// é essa claim que os endpoints com [Authorize(Roles = "Admin")] exigem.
    /// </summary>
    public string GenerateAdminToken(AdminUser admin)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, admin.Email),
            new Claim(ClaimTypes.Name, admin.Name),
            new Claim(ClaimTypes.Role, "Admin"),
        };

        return BuildToken(claims);
    }

    /// <summary>
    /// Token de suporte: role "Suporte" emitida separadamente — o suporte existe
    /// justamente para ver o que o dono da loja não vê, e rodar atrás do login
    /// de admin inverte essa separação.
    /// </summary>
    public string GenerateSuporteToken(SuporteUser suporte)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, suporte.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, suporte.Email),
            new Claim(ClaimTypes.Name, suporte.Name),
            new Claim(ClaimTypes.Role, "Suporte"),
        };

        return BuildToken(claims);
    }

    private string BuildToken(IEnumerable<Claim> claims)
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection["Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
