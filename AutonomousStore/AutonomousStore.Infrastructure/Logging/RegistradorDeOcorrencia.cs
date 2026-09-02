using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace AutonomousStore.Infrastructure.Logging;

/// <summary>
/// Grava a ocorrencia, e engole a propria falha.
/// </summary>
/// <remarks>
/// REGISTRAR UM PROBLEMA NAO PODE VIRAR UM PROBLEMA.
///
/// Quem chama isto e o `VerifyExit` da leitora da porta, o
/// `DetectShelfChange` da camera, o middleware de excecao. Se a gravacao
/// estourar — banco fora, migracao nao aplicada, coluna faltando — e a
/// excecao subir, a PORTA DA LOJA para de funcionar por causa do sistema que
/// existe para vigiar a loja.
///
/// Entao tudo aqui e `try/catch` e devolve `null`. A falha vai para o
/// `ILogger`, que e o unico canal que nao depende do banco.
///
/// O mesmo desenho do `GravarPerguntaAsync` no gerente: nao conseguir anotar
/// a pergunta nunca pode impedir a resposta.
/// </remarks>
public class RegistradorDeOcorrencia : IRegistradorDeOcorrencia
{
    private readonly IOcorrenciaRepository _repositorio;
    private readonly ILogger<RegistradorDeOcorrencia> _log;

    public RegistradorDeOcorrencia(
        IOcorrenciaRepository repositorio,
        ILogger<RegistradorDeOcorrencia> log)
    {
        _repositorio = repositorio;
        _log = log;
    }

    public async Task<Guid?> RegistrarAsync(
        Ocorrencia ocorrencia, CancellationToken cancellationToken = default)
    {
        try
        {
            // O MESMO FATO DE NOVO AGORA SOMA, NAO SUMIA.
            //
            // Antes, repeticao era DESCARTADA aqui — e com isso a segunda vez
            // e a milesima ficavam iguais a nenhuma. O repositorio agora
            // devolve a linha que ficou valendo (a nova, ou a antiga com o
            // contador somado), e o Chefe passa a enxergar a diferenca entre
            // "aconteceu" e "esta acontecendo sem parar".
            var valendo = await _repositorio.RegistrarOuSomarAsync(ocorrencia, cancellationToken);
            await _repositorio.SaveChangesAsync(cancellationToken);
            return valendo.Id;
        }
        catch (Exception e)
        {
            // O log e o unico canal que nao depende do banco — que e
            // justamente o que pode estar quebrado aqui.
            _log.LogError(e,
                "Não consegui gravar a ocorrência {Tipo} de {Modulo}.{Operacao}: {Descricao}",
                ocorrencia.Tipo, ocorrencia.Modulo, ocorrencia.Operacao, ocorrencia.Descricao);
            return null;
        }
    }

    public async Task<int> RegistrarVariasAsync(
        IEnumerable<Ocorrencia> ocorrencias, CancellationToken cancellationToken = default)
    {
        var lista = ocorrencias.ToList();
        if (lista.Count == 0) return 0;

        try
        {
            // Uma por vez de proposito, e nao AddRange: o `RegistrarOuSomar`
            // precisa ver se a anterior do MESMO lote ja entrou. Em bloco,
            // duas iguais na mesma varredura passariam as duas e o indice
            // unico derrubaria o lote inteiro.
            var gravadas = 0;
            foreach (var o in lista)
            {
                var valendo = await _repositorio.RegistrarOuSomarAsync(o, cancellationToken);
                if (ReferenceEquals(valendo, o)) gravadas++;
            }

            await _repositorio.SaveChangesAsync(cancellationToken);
            return gravadas;
        }
        catch (Exception e)
        {
            _log.LogError(e, "Não consegui gravar {Quantidade} ocorrência(s).", lista.Count);
            return 0;
        }
    }
}
