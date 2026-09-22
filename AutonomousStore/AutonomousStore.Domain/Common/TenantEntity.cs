namespace AutonomousStore.Domain.Common;

/// <summary>
/// Entidade que pertence a uma empresa assinante (tenant). É a etiqueta que o
/// filtro global usa para isolar uma empresa da outra.
///
/// Quem carimba o <see cref="TenantId"/> é a infraestrutura, ao salvar: a
/// empresa da requisição vira a dona do registro novo. O código de negócio não
/// escolhe empresa — exceto quem opera a plataforma, que usa
/// <see cref="PertenceAoTenant"/> para gravar dado em nome de uma empresa.
/// </summary>
public abstract class TenantEntity : Entity
{
    /// <summary>
    /// Nulo só até o primeiro salvamento (e em <see cref="Entities.Ocorrencia"/>,
    /// que também registra eventos da plataforma, sem empresa).
    /// </summary>
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// Define a empresa dona. Só vale uma vez: mudar um registro de empresa é
    /// exatamente o vazamento que o isolamento existe para impedir.
    /// </summary>
    public void PertenceAoTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("A empresa não pode ser vazia.", nameof(tenantId));

        if (TenantId is { } atual && atual != tenantId)
            throw new InvalidOperationException("Este registro já pertence a outra empresa.");

        TenantId = tenantId;
    }
}
