using System.Security.Claims;
using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Tenancy;
using Microsoft.Extensions.Caching.Memory;

namespace AutonomousStore.WebApi.Tenancy;

public enum ResultadoDaResolucao
{
    /// <summary>A requisição age em nome de uma empresa.</summary>
    Empresa,

    /// <summary>Criador ou Técnico: enxerga todas as empresas.</summary>
    Plataforma,

    /// <summary>Ninguém disse de qual empresa é. O filtro devolve nada.</summary>
    NaoIdentificada,

    EmpresaNaoEncontrada,

    /// <summary>A empresa existe, mas o Criador cortou o acesso dela.</summary>
    EmpresaSuspensa,
}

public readonly record struct ResolucaoDeEmpresa(ResultadoDaResolucao Resultado, Guid? EmpresaId = null);

/// <summary>
/// Descobre de qual empresa é uma requisição — a única decisão de segurança
/// que o resto do sistema não refaz. Tudo o que vem depois (filtro do banco,
/// carimbo ao gravar) confia no que sai daqui.
/// </summary>
public class TenantResolver
{
    // Curto de proposito: e o tempo que uma suspensao leva para valer em uma
    // requisicao que ja estava em andamento. Maior que zero para nao perguntar
    // ao banco, a cada chamada, se a empresa ainda existe.
    private static readonly TimeSpan ValidadeDaEmpresa = TimeSpan.FromSeconds(30);

    // Quem digita um slug que nao existe (ou o adivinha) nao pode transformar
    // cada tentativa em uma consulta ao banco.
    private static readonly TimeSpan ValidadeDoNaoEncontrado = TimeSpan.FromSeconds(10);

    private readonly ITenantRepository _empresas;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuracao;

    public TenantResolver(ITenantRepository empresas, IMemoryCache cache, IConfiguration configuracao)
    {
        _empresas = empresas;
        _cache = cache;
        _configuracao = configuracao;
    }

    private sealed record EmpresaEmCache(Guid Id, string Slug, bool Ativa);

    /// <param name="usuario">Quem chamou (pode estar anônimo).</param>
    /// <param name="slugDoLink">
    /// O que o app mandou no cabeçalho <see cref="Papeis.CabecalhoEmpresa"/>.
    /// Só vale para quem ainda não tem token com empresa.
    /// </param>
    public async Task<ResolucaoDeEmpresa> ResolverAsync(
        ClaimsPrincipal? usuario, string? slugDoLink, CancellationToken cancellationToken = default)
    {
        var autenticado = usuario?.Identity?.IsAuthenticated == true;

        if (autenticado)
        {
            // Quem opera a plataforma nao tem empresa: atende todas.
            if (usuario!.IsInRole(Papeis.Criador) || usuario.IsInRole(Papeis.Suporte))
                return new(ResultadoDaResolucao.Plataforma);

            // O TOKEN e a unica fonte da verdade sobre a empresa de quem entrou.
            // O cabecalho nunca a substitui: senao um Admin da empresa A mandaria
            // "X-Empresa: b" e passaria a agir como Admin da empresa B.
            if (Guid.TryParse(usuario.FindFirstValue(Papeis.ClaimEmpresa), out var doToken))
                return await ResolverPorIdAsync(doToken, cancellationToken);

            // Token emitido ANTES do multiempresa: nao tem a claim. So pode ser
            // da empresa padrao — o unico lugar onde existia alguem antes. Por isso
            // o cabecalho tambem e ignorado aqui, pelo mesmo motivo de cima.
            // (Some sozinho: o token vale 7 dias.)
            return await ResolverPorSlugAsync(_configuracao["Tenancy:EmpresaPadrao"], cancellationToken);
        }

        // Anonimo: a empresa vem do link ou QR code da loja. Na transicao, quando
        // o app ainda nao manda o cabecalho, vale a empresa padrao configurada.
        var alvo = string.IsNullOrWhiteSpace(slugDoLink)
            ? _configuracao["Tenancy:EmpresaPadrao"]
            : slugDoLink;

        return await ResolverPorSlugAsync(alvo, cancellationToken);
    }

    /// <summary>Faz o cache esquecer a empresa — chamado quando o Criador a suspende, reativa ou renomeia.</summary>
    public void Esquecer(Tenant empresa)
    {
        _cache.Remove(ChavePorId(empresa.Id));
        _cache.Remove(ChavePorSlug(empresa.Slug));
    }

    /// <summary>Aplica o resultado ao contexto da requisição. Quem não foi identificado fica sem empresa (e sem ver nada).</summary>
    public static void Aplicar(ResolucaoDeEmpresa resolucao, TenantContext contexto)
    {
        switch (resolucao.Resultado)
        {
            case ResultadoDaResolucao.Empresa when resolucao.EmpresaId is { } id:
                contexto.DefinirEmpresa(id);
                break;

            case ResultadoDaResolucao.Plataforma:
                contexto.DefinirTodasAsEmpresas();
                break;
        }
    }

    private async Task<ResolucaoDeEmpresa> ResolverPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var empresa = await _cache.GetOrCreateAsync(ChavePorId(id), async entrada =>
        {
            var achada = await _empresas.GetByIdAsync(id, cancellationToken);
            entrada.AbsoluteExpirationRelativeToNow = achada is null ? ValidadeDoNaoEncontrado : ValidadeDaEmpresa;
            return achada is null ? null : new EmpresaEmCache(achada.Id, achada.Slug, achada.EstaAtiva);
        });

        return Traduzir(empresa);
    }

    private async Task<ResolucaoDeEmpresa> ResolverPorSlugAsync(string? slug, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return new(ResultadoDaResolucao.NaoIdentificada);

        var normalizado = slug.Trim().ToLowerInvariant();

        var empresa = await _cache.GetOrCreateAsync(ChavePorSlug(normalizado), async entrada =>
        {
            var achada = await _empresas.GetBySlugAsync(normalizado, cancellationToken);
            entrada.AbsoluteExpirationRelativeToNow = achada is null ? ValidadeDoNaoEncontrado : ValidadeDaEmpresa;
            return achada is null ? null : new EmpresaEmCache(achada.Id, achada.Slug, achada.EstaAtiva);
        });

        return Traduzir(empresa);
    }

    private static ResolucaoDeEmpresa Traduzir(EmpresaEmCache? empresa) => empresa switch
    {
        null => new(ResultadoDaResolucao.EmpresaNaoEncontrada),
        { Ativa: false } => new(ResultadoDaResolucao.EmpresaSuspensa, empresa.Id),
        _ => new(ResultadoDaResolucao.Empresa, empresa.Id),
    };

    private static string ChavePorId(Guid id) => $"empresa:id:{id}";
    private static string ChavePorSlug(string slug) => $"empresa:slug:{slug}";
}
