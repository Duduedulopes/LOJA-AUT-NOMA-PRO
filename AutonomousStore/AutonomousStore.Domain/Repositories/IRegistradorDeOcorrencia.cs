using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

/// <summary>
/// Grava uma ocorrencia sem NUNCA derrubar quem chamou.
/// </summary>
/// <remarks>
/// A REGRA QUE DEFINE ESTA INTERFACE: registrar um problema nao pode virar
/// um problema.
///
/// O `VerifyExit` e chamado pela leitora da porta, e a leitora precisa de
/// resposta em milissegundos para acender verde ou vermelho. Se a gravacao
/// da ocorrencia estourar — banco fora do ar, coluna faltando, migracao nao
/// aplicada — e a excecao subir, a porta para de funcionar POR CAUSA DO
/// SISTEMA DE VIGIA. O remedio teria matado o paciente.
///
/// Entao toda implementacao engole a propria falha e devolve `null`. O mesmo
/// desenho ja usado no `GravarPerguntaAsync` do gerente: nao conseguir
/// anotar a pergunta nao pode impedir a resposta.
/// </remarks>
public interface IRegistradorDeOcorrencia
{
    /// <summary>
    /// Grava. Devolve o id, ou `null` quando nao deu — porque falhou, ou
    /// porque a chave ja existia e isto seria a mesma ocorrencia de novo.
    /// </summary>
    Task<Guid?> RegistrarAsync(Ocorrencia ocorrencia, CancellationToken cancellationToken = default);

    /// <summary>Grava varias. Mesma regra: nao estoura.</summary>
    Task<int> RegistrarVariasAsync(IEnumerable<Ocorrencia> ocorrencias, CancellationToken cancellationToken = default);
}
