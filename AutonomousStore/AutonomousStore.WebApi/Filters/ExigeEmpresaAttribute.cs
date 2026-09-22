using AutonomousStore.Domain.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AutonomousStore.WebApi.Filters;

/// <summary>
/// Para rotas que só fazem sentido DENTRO de uma empresa e que podem ser
/// chamadas sem login (cadastro e login do comprador). Sem empresa definida,
/// responde com uma mensagem que o app consegue mostrar — em vez de deixar o
/// filtro do banco devolver "e-mail não encontrado", que mentiria sobre o
/// motivo.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ExigeEmpresaAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext contexto)
    {
        var tenant = contexto.HttpContext.RequestServices.GetRequiredService<ITenantContext>();

        if (tenant.TenantId is null)
        {
            contexto.Result = new BadRequestObjectResult(new
            {
                error = "Não sei de qual empresa é este acesso. Abra o app pelo link ou pelo QR code da loja.",
            });
        }
    }
}
