namespace AutonomousStore.Domain.Common;

/// <summary>
/// Diz em nome de qual empresa a requisição atual age. É a peça que o
/// DbContext lê para filtrar tudo por empresa — sem ela, nenhum dado de
/// empresa é visível.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// A empresa da requisição. Nulo quando ninguém definiu uma — e nesse
    /// caso o filtro devolve NADA, em vez de devolver tudo. Falhar fechado é
    /// de propósito: esquecer de definir a empresa deve dar tela vazia, e não
    /// vazar dado de outra empresa.
    /// </summary>
    Guid? TenantId { get; }

    /// <summary>
    /// Verdadeiro para quem opera a plataforma (Criador e Técnico): enxerga
    /// todas as empresas. Nunca é verdadeiro para Admin nem Comprador.
    /// </summary>
    bool VeTodasAsEmpresas { get; }
}
