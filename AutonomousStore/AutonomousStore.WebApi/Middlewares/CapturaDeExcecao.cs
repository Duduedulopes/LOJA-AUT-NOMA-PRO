using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using Microsoft.AspNetCore.Diagnostics;

namespace AutonomousStore.WebApi.Middlewares;

/// <summary>
/// Toda exceção que ninguém tratou vira uma ocorrência antes de virar um 500.
/// </summary>
/// <remarks>
/// O QUE EXISTIA ANTES DISTO: NADA.
///
/// O `Deteccoes.ErroDeExecucao` estava escrito e testado desde que as
/// ocorrências nasceram, e nunca foi chamado por ninguém. Na prática, um erro
/// dentro da WebApi sumia: o cliente recebia um 500 sem explicação, e o
/// histórico do suporte não registrava que tinha acontecido. O erro mais caro
/// de achar é o que não deixa rastro.
///
/// DUAS REGRAS QUE ESTE ARQUIVO NÃO PODE QUEBRAR.
///
/// 1. Gravar o erro NÃO PODE virar um segundo erro. Se o banco estiver fora —
///    que é justamente uma das causas prováveis de estarmos aqui — a tentativa
///    de registrar estouraria por cima da exceção original e apagaria a causa
///    de verdade. Por isso tudo é `try/catch`, e a falha de gravação vai para
///    o `ILogger`, o único canal que não depende do banco.
///
/// 2. A resposta continua sendo um 500. Isto REGISTRA, não conserta: quem
///    chamou tem de continuar sabendo que deu errado.
///
/// O CÓDIGO QUE VAI NA RESPOSTA.
///
/// O corpo devolve o `correlationId`. É o número que a barra vermelha mostra
/// para o Eduardo e que o suporte cola no filtro do histórico para puxar tudo
/// o que aconteceu naquele mesmo pedido — o erro do servidor e o do navegador
/// lado a lado, se o navegador tiver mandado o mesmo cabeçalho.
/// </remarks>
public sealed class CapturaDeExcecao : IExceptionHandler
{
    /// <summary>O cabeçalho que amarra o erro do navegador ao erro do servidor.</summary>
    public const string CabecalhoCorrelacao = "X-Correlation-Id";

    private readonly ILogger<CapturaDeExcecao> _log;

    public CapturaDeExcecao(ILogger<CapturaDeExcecao> log) => _log = log;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext contexto, Exception erro, CancellationToken cancellationToken)
    {
        var correlacao = Correlacao(contexto);

        // O log SEMPRE, antes de qualquer coisa que dependa do banco.
        _log.LogError(erro, "Erro não tratado em {Metodo} {Caminho}. Correlação: {Correlacao}",
            contexto.Request.Method, contexto.Request.Path, correlacao);

        try
        {
            // Do escopo DESTA requisição: o handler é singleton, o
            // registrador é scoped. Pedir no construtor prenderia um escopo
            // morto e daria erro de captive dependency.
            var registrador = contexto.RequestServices.GetService<IRegistradorDeOcorrencia>();
            if (registrador is not null)
            {
                await registrador.RegistrarAsync(
                    Deteccoes.ErroDeExecucao(
                        caminho: contexto.Request.Path.Value ?? "/",
                        metodoHttp: contexto.Request.Method,
                        erro: erro,
                        correlationId: correlacao,
                        quandoUtc: DateTime.UtcNow),
                    cancellationToken);
            }
        }
        catch (Exception aoGravar)
        {
            _log.LogError(aoGravar, "E ainda falhei ao registrar a ocorrência do erro acima.");
        }

        // Se a resposta já começou a sair, mexer nela estoura. Devolver
        // `false` deixa o servidor derrubar a conexão, que é o certo aqui:
        // meia resposta com um 500 colado no fim é pior que nenhuma.
        if (contexto.Response.HasStarted) return false;

        contexto.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await contexto.Response.WriteAsJsonAsync(new
        {
            // Sem a mensagem da exceção: ela pode conter nome de tabela,
            // caminho de arquivo e trecho de consulta. Isso fica na
            // ocorrência, que só o admin e o suporte leem.
            erro = "Alguma coisa quebrou do nosso lado. Já ficou registrado.",
            correlationId = correlacao,
        }, cancellationToken);

        return true;
    }

    private static Guid Correlacao(HttpContext contexto)
        => contexto.Request.Headers.TryGetValue(CabecalhoCorrelacao, out var v)
           && Guid.TryParse(v.ToString(), out var g)
            ? g
            : Guid.NewGuid();
}
