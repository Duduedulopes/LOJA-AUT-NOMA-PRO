using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Ocorrencias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

/// <summary>
/// O que o sistema percebeu de errado e guardou.
/// </summary>
/// <remarks>
/// NADA AQUI E ABERTO. Uma ocorrencia de roubo diz qual tag passou pela porta
/// e a que horas; uma de erro de execucao diz o caminho do modulo que
/// quebrou. Nenhum dos dois e coisa de endpoint publico — e o `VerifyExit`,
/// que e `[AllowAnonymous]` porque quem chama e a leitora da porta, GRAVA
/// aqui mas nao LE daqui.
///
/// DOIS PAPEIS, E DE PROPOSITO. `Admin` e o dono da loja: cuida da loja
/// dele. `Suporte` e o tecnico que atende varias lojas — o papel existe
/// justamente porque ele precisa ver o que o dono nao ve, e nao pode entrar
/// com a credencial do dono para isso.
///
/// O papel `Suporte` ainda nao e emitido por ninguem: o
/// `SuporteAuthController` faz parte da Parte 2. Deixar o nome aqui desde ja
/// nao abre nada — token sem essa claim continua barrado — e evita que a
/// outra metade fique parada esperando uma linha num arquivo meu.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Suporte")]
public class OcorrenciasController : ControllerBase
{
    /// <summary>O nome do freio da rota anônima. Configurado no Program.cs.</summary>
    public const string FreioDeRelato = "relato-de-erro";

    private readonly IOcorrenciaRepository _ocorrencias;
    private readonly IRegistradorDeOcorrencia _registrador;

    public OcorrenciasController(
        IOcorrenciaRepository ocorrencias, IRegistradorDeOcorrencia registrador)
    {
        _ocorrencias = ocorrencias;
        _registrador = registrador;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OcorrenciaResponse>>> Buscar(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? ate,
        [FromQuery] TipoDeOcorrencia? tipo,
        [FromQuery] Severidade? severidade,
        [FromQuery] EstadoDaOcorrencia? estado,
        [FromQuery] Guid? correlationId,
        [FromQuery] int limite,
        CancellationToken cancellationToken)
    {
        var filtro = new FiltroDeOcorrencia(
            Desde: desde,
            Ate: ate,
            Tipo: tipo,
            SeveridadeMinima: severidade,
            Estado: estado,
            CorrelationId: correlationId,
            Limite: limite <= 0 ? 200 : limite);

        var achadas = await _ocorrencias.BuscarAsync(filtro, cancellationToken);
        return Ok(achadas.Select(ToResponse).ToList());
    }

    /// <summary>O contador do sino.</summary>
    [HttpGet("nao-vistas")]
    public async Task<ActionResult<NaoVistasResponse>> NaoVistas(CancellationToken cancellationToken)
    {
        var (total, criticas, maisRecente) = await _ocorrencias.NaoVistasAsync(cancellationToken);
        return Ok(new NaoVistasResponse(total, criticas, maisRecente));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OcorrenciaResponse>> PorId(Guid id, CancellationToken cancellationToken)
    {
        var o = await _ocorrencias.GetByIdAsync(id, cancellationToken);
        return o is null ? NotFound() : Ok(ToResponse(o));
    }

    [HttpPost("{id:guid}/vista")]
    public async Task<ActionResult<OcorrenciaResponse>> Vista(Guid id, CancellationToken cancellationToken)
    {
        var o = await _ocorrencias.GetByIdAsync(id, cancellationToken);
        if (o is null) return NotFound();

        o.MarcarVista();
        await _ocorrencias.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(o));
    }

    [HttpPost("{id:guid}/resolver")]
    public async Task<ActionResult<OcorrenciaResponse>> Resolver(
        Guid id, ResolverRequest request, CancellationToken cancellationToken)
    {
        var o = await _ocorrencias.GetByIdAsync(id, cancellationToken);
        if (o is null) return NotFound();

        // QUEM RESOLVEU vem do token, nao do corpo do pedido. Deixar o
        // cliente dizer quem foi e deixar o cliente assinar em nome de
        // outro.
        var quem = User.Identity?.Name ?? User.FindFirst("email")?.Value;

        o.Resolver(quem, request.Nota);
        await _ocorrencias.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(o));
    }

    [HttpPost("{id:guid}/suporte")]
    public async Task<ActionResult<OcorrenciaResponse>> Suporte(
        Guid id, SuporteRequest request, CancellationToken cancellationToken)
    {
        var o = await _ocorrencias.GetByIdAsync(id, cancellationToken);
        if (o is null) return NotFound();

        o.EnviarAoSuporte(request.DescricaoDoAdmin);
        await _ocorrencias.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(o));
    }

    [HttpGet("resumo")]
    public async Task<ActionResult<ResumoResponse>> Resumo(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? ate,
        CancellationToken cancellationToken)
    {
        var fim = ate ?? DateTime.UtcNow;
        var ini = desde ?? fim.AddDays(-30);

        var linhas = await _ocorrencias.ResumoAsync(ini, fim, cancellationToken);

        return Ok(new ResumoResponse(
            ini, fim,
            linhas.Sum(l => l.Quantidade),
            linhas.Select(l => new ResumoLinha(l.Tipo.ToString(), l.Severidade.ToString(), l.Quantidade)).ToList()));
    }

    /// <summary>
    /// Um erro que estourou no navegador — do cliente, do admin ou do suporte.
    /// </summary>
    /// <remarks>
    /// A ÚNICA ROTA ANÔNIMA DESTE CONTROLADOR, E A ÚNICA QUE SÓ ESCREVE.
    ///
    /// Ela não devolve ocorrência nenhuma: recebe uma e responde com o número
    /// dela. Quem não tem login não fica sabendo o que já existe no
    /// histórico — só consegue acrescentar. É a mesma forma do `VerifyExit`,
    /// que a leitora da porta chama sem token: grava aqui, não lê daqui.
    ///
    /// O freio (`relato-de-erro`) limita por IP. Sem ele, uma rota anônima que
    /// escreve no banco é um convite: bastaria um laço para encher a tabela
    /// que o Eduardo usa para enxergar a loja.
    /// </remarks>
    [AllowAnonymous]
    [EnableRateLimiting(FreioDeRelato)]
    [HttpPost("navegador")]
    [ProducesResponseType(typeof(RelatoDeErroResponse), StatusCodes.Status202Accepted)]
    public async Task<ActionResult<RelatoDeErroResponse>> Navegador(
        RelatoDeErroRequest request, CancellationToken cancellationToken)
    {
        Ocorrencia nova;
        try
        {
            nova = Deteccoes.ErroNoNavegador(
                app: request.App,
                pagina: request.Pagina,
                mensagem: request.Mensagem,
                pilha: request.Pilha,
                navegador: request.Navegador,
                correlationId: request.CorrelationId ?? Guid.NewGuid(),
                quandoUtc: DateTime.UtcNow);
        }
        catch (ArgumentException e)
        {
            // App fora da lista, ou campo obrigatório vazio. 400 e ponto: não
            // vale gravar ocorrência sobre um relato malformado, senão a
            // própria defesa vira o entulho que ela evita.
            return BadRequest(new { erro = e.Message });
        }

        // O registrador engole a própria falha e devolve null: não conseguir
        // gravar o relato não pode virar um erro em cima do erro.
        var id = await _registrador.RegistrarAsync(nova, cancellationToken);
        if (id is null)
            return Accepted(new RelatoDeErroResponse(Guid.Empty, nova.CorrelationId, false, 0));

        // A LINHA QUE FICOU VALENDO PODE NÃO SER A QUE EU ACABEI DE MONTAR.
        //
        // Se este mesmo erro já estava na tabela, o registrador somou na linha
        // ANTIGA e descartou a nova. Escalar antes de gravar — que era como
        // isto estava escrito — mandava para o suporte um objeto que ia para o
        // lixo, e o botão não fazia nada. Silenciosamente, que é o pior jeito.
        //
        // E é exatamente o caminho normal do botão: o erro já foi reportado
        // sozinho quando aconteceu, então quando alguém clica em "reportar" a
        // linha SEMPRE existe.
        var valendo = await _ocorrencias.GetByIdAsync(id.Value, cancellationToken) ?? nova;

        if (request.ParaOSuporte && valendo.Estado != EstadoDaOcorrencia.NoSuporte)
        {
            var nota = string.IsNullOrWhiteSpace(request.Contato)
                ? "Reportado pelo botão da barra de erro."
                : $"Reportado pelo botão da barra de erro. Contato: {Curto(request.Contato, 400)}";

            valendo.EnviarAoSuporte(nota);
            await _ocorrencias.SaveChangesAsync(cancellationToken);
        }

        return Accepted(new RelatoDeErroResponse(
            valendo.Id,
            valendo.CorrelationId,
            valendo.Estado == EstadoDaOcorrencia.NoSuporte,
            valendo.VezesVistas));
    }

    private static string Curto(string s, int limite) => s.Length <= limite ? s : s[..limite];

    private static OcorrenciaResponse ToResponse(Ocorrencia o) => new(
        o.Id,
        o.QuandoUtc,
        o.Sistema,
        o.Modulo,
        o.Operacao,
        o.Tipo.ToString(),
        o.Severidade.ToString(),
        o.Descricao,
        o.DadosEnvolvidosJson,
        o.SequenciaJson,
        o.CausaProvavel,
        o.CausaRaiz,
        o.Impacto,
        o.Recomendacao.ToString(),
        o.AcaoExecutada,
        o.Resultado,
        o.Estado.ToString(),
        o.CorrelationId,
        o.VistaEm,
        o.ResolvidaEm,
        o.ResolvidaPor,
        o.NotaDoAdmin,
        o.VezesVistas,
        o.UltimaVezUtc);
}
