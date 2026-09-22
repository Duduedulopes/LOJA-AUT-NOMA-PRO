using AutonomousStore.Domain.Common;
using AutonomousStore.Infrastructure.Tenancy;

namespace AutonomousStore.WebApi.Tenancy;

/// <summary>
/// Diz, no começo de cada requisição, de qual empresa ela é. Tem de rodar
/// DEPOIS da autenticação (precisa das claims do token) e ANTES de qualquer
/// controller (que já consulta o banco).
/// </summary>
public class TenantMiddleware
{
    private readonly RequestDelegate _proximo;

    public TenantMiddleware(RequestDelegate proximo) => _proximo = proximo;

    public async Task InvokeAsync(HttpContext contexto, TenantResolver resolvedor, TenantContext tenant)
    {
        var slug = contexto.Request.Headers[Papeis.CabecalhoEmpresa].FirstOrDefault();

        var resolucao = await resolvedor.ResolverAsync(contexto.User, slug, contexto.RequestAborted);

        if (resolucao.Resultado == ResultadoDaResolucao.EmpresaSuspensa)
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            await contexto.Response.WriteAsJsonAsync(new
            {
                error = "O acesso desta empresa está suspenso. Fale com o suporte.",
            });
            return;
        }

        if (resolucao.Resultado == ResultadoDaResolucao.EmpresaNaoEncontrada)
        {
            contexto.Response.StatusCode = StatusCodes.Status404NotFound;
            await contexto.Response.WriteAsJsonAsync(new { error = "Empresa não encontrada." });
            return;
        }

        TenantResolver.Aplicar(resolucao, tenant);

        await _proximo(contexto);
    }
}
