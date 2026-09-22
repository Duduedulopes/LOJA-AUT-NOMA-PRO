using AutonomousStore.Infrastructure.Tenancy;
using Microsoft.AspNetCore.SignalR;

namespace AutonomousStore.WebApi.Tenancy;

/// <summary>
/// O par do <see cref="TenantMiddleware"/> para o SignalR.
///
/// Uma chamada de hub NÃO passa pelo pipeline HTTP: o WebSocket é aberto uma
/// vez e depois cada método roda em um escopo de serviços próprio, onde o
/// contexto de empresa nasce vazio. Sem este filtro, o chat do Admin abriria
/// e não enxergaria chamado nenhum — o filtro do banco falha fechado.
///
/// Repare que o filtro não guarda serviço no construtor: cada invocação tem o
/// seu escopo, e é dele que a empresa é lida e aplicada.
/// </summary>
public class TenantHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext contexto, Func<HubInvocationContext, ValueTask<object?>> proximo)
    {
        await AplicarAsync(contexto.ServiceProvider, contexto.Context);
        return await proximo(contexto);
    }

    public async Task OnConnectedAsync(
        HubLifetimeContext contexto, Func<HubLifetimeContext, Task> proximo)
    {
        await AplicarAsync(contexto.ServiceProvider, contexto.Context);
        await proximo(contexto);
    }

    private static async Task AplicarAsync(IServiceProvider servicos, HubCallerContext conexao)
    {
        var resolvedor = servicos.GetRequiredService<TenantResolver>();
        var tenant = servicos.GetRequiredService<TenantContext>();

        // Sem cabecalho: o hub so aceita quem tem token, e a empresa esta nele.
        var resolucao = await resolvedor.ResolverAsync(conexao.User, slugDoLink: null, conexao.ConnectionAborted);

        if (resolucao.Resultado == ResultadoDaResolucao.EmpresaSuspensa)
            throw new HubException("O acesso desta empresa está suspenso. Fale com o suporte.");

        if (resolucao.Resultado == ResultadoDaResolucao.EmpresaNaoEncontrada)
            throw new HubException("Empresa não encontrada.");

        TenantResolver.Aplicar(resolucao, tenant);
    }
}
