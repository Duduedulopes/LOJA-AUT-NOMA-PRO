using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Repositories;

/// <summary>Filtro de busca de ocorrencia. Todo campo e opcional.</summary>
/// <remarks>
/// Um record em vez de oito parametros soltos: a tela do suporte vai crescer
/// filtro, e assinatura de metodo com dez argumentos e onde alguem troca dois
/// de lugar sem o compilador reclamar.
/// </remarks>
public record FiltroDeOcorrencia(
    DateTime? Desde = null,
    DateTime? Ate = null,
    TipoDeOcorrencia? Tipo = null,
    Severidade? SeveridadeMinima = null,
    EstadoDaOcorrencia? Estado = null,
    Guid? CorrelationId = null,
    int Limite = 200);

public interface IOcorrenciaRepository
{
    Task AddAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava varias de uma vez. O varredor do `Cancel` acha dezenas numa
    /// passada; uma ida ao banco por ocorrencia seria dezenas de idas.
    /// </summary>
    Task AddRangeAsync(IEnumerable<Ocorrencia> ocorrencias, CancellationToken cancellationToken = default);

    Task<Ocorrencia?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Mais recente primeiro.</summary>
    Task<IReadOnlyList<Ocorrencia>> BuscarAsync(FiltroDeOcorrencia filtro, CancellationToken cancellationToken = default);

    /// <summary>Quantas ainda estao em <see cref="EstadoDaOcorrencia.Nova"/>, e quantas dessas sao criticas.</summary>
    Task<(int Total, int Criticas, DateTime? MaisRecente)> NaoVistasAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(TipoDeOcorrencia Tipo, Severidade Severidade, int Quantidade)>>
        ResumoAsync(DateTime desde, DateTime ate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ja existe ocorrencia deste tipo para esta chave?
    /// </summary>
    /// <remarks>
    /// EVITA A MESMA OCORRENCIA CEM VEZES. O varredor do `Cancel` roda a cada
    /// consulta; sem isto, a mesma sessao cancelada geraria uma linha nova a
    /// cada vez que alguem perguntasse "tivemos algum furo?", e o sino
    /// marcaria centenas de alertas que sao um so.
    /// </remarks>
    Task<bool> JaExisteAsync(TipoDeOcorrencia tipo, string chave, CancellationToken cancellationToken = default);

    /// <summary>
    /// Grava a ocorrencia — ou, se este mesmo fato ja esta na tabela, SOMA
    /// nele em vez de criar uma linha nova. Devolve a linha que ficou valendo.
    /// </summary>
    /// <remarks>
    /// POR QUE ISTO E DO REPOSITORIO E NAO DE QUEM CHAMA.
    ///
    /// A tabela tem indice UNICO em `Chave`. Quem gravasse direto teria de
    /// lembrar de consultar antes, e o dia em que alguem esquecesse nao daria
    /// linha duplicada: daria EXCECAO no meio de um pedido, por causa de um
    /// alerta. O detector nao pode derrubar a operacao que ele esta vigiando.
    ///
    /// Ocorrencia sem chave sempre entra como nova — e o caso de quem nao tem
    /// como se identificar, e a duvida ali pesa a favor de registrar.
    /// </remarks>
    Task<Ocorrencia> RegistrarOuSomarAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken = default);

    /// <summary>O chamado COM a conversa dentro.</summary>
    /// <remarks>
    /// Separado do `GetByIdAsync` de propósito. A fila e o sino leem
    /// ocorrência a toda hora e não precisam das mensagens; trazer a conversa
    /// junto sempre seria pagar por um dado que quase ninguém usa, na consulta
    /// mais frequente do sistema.
    /// </remarks>
    Task<Ocorrencia?> ObterComConversaAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Os chamados que ESTA pessoa abriu, do mais recente para o mais antigo.</summary>
    Task<IReadOnlyList<Ocorrencia>> ChamadosDeAsync(string email, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
