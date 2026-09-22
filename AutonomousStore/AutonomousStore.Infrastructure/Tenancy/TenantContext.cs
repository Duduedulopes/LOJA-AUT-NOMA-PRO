using AutonomousStore.Domain.Common;

namespace AutonomousStore.Infrastructure.Tenancy;

/// <summary>
/// A empresa da requisição em andamento. Uma instância por requisição (escopo),
/// preenchida logo no começo do pipeline e lida pelo DbContext a cada consulta.
///
/// Nasce SEM empresa e SEM poder de ver todas: quem não foi identificado não
/// enxerga nada. Só as duas chamadas abaixo abrem alguma porta.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }

    public bool VeTodasAsEmpresas { get; private set; }

    /// <summary>A requisição age em nome de uma empresa (Admin, Comprador, app com link da loja).</summary>
    public void DefinirEmpresa(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A empresa não pode ser vazia.", nameof(tenantId));

        TenantId = tenantId;
        VeTodasAsEmpresas = false;
    }

    /// <summary>A requisição é de quem opera a plataforma (Criador, Técnico).</summary>
    public void DefinirTodasAsEmpresas()
    {
        TenantId = null;
        VeTodasAsEmpresas = true;
    }
}
