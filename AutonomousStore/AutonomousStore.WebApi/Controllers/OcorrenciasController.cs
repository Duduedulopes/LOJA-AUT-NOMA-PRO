using System.Security.Claims;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Ocorrencias;
using AutonomousStore.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using AutonomousStore.Domain.Common;

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
[Authorize(Roles = Papeis.Admin + "," + Papeis.DaPlataforma)]
public class OcorrenciasController : ControllerBase
{
    /// <summary>O nome do freio da rota anônima. Configurado no Program.cs.</summary>
    public const string FreioDeRelato = "relato-de-erro";

    private readonly IOcorrenciaRepository _ocorrencias;
    private readonly IRegistradorDeOcorrencia _registrador;
    private readonly ITenantRepository _empresas;

    public OcorrenciasController(
        IOcorrenciaRepository ocorrencias, IRegistradorDeOcorrencia registrador, ITenantRepository empresas)
    {
        _ocorrencias = ocorrencias;
        _registrador = registrador;
        _empresas = empresas;
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
            Limite: limite <= 0 ? 200 : limite,
            EmailDoAdmin: EmailSeForAdmin);

        var achadas = await _ocorrencias.BuscarAsync(filtro, cancellationToken);
        var rotulos = await RotulosAsync(achadas, cancellationToken);
        return Ok(achadas.Select(o => ToResponse(o, rotulos)).ToList());
    }

    /// <summary>O contador do sino.</summary>
    [HttpGet("nao-vistas")]
    public async Task<ActionResult<NaoVistasResponse>> NaoVistas(CancellationToken cancellationToken)
    {
        var (total, criticas, maisRecente) = await _ocorrencias.NaoVistasAsync(EmailSeForAdmin, cancellationToken);
        return Ok(new NaoVistasResponse(total, criticas, maisRecente));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OcorrenciaResponse>> PorId(Guid id, CancellationToken cancellationToken)
    {
        var o = await _ocorrencias.GetByIdAsync(id, cancellationToken);
        return o is null || !Enxerga(o) ? NotFound() : Ok(ToResponse(o, await RotulosAsync([o], cancellationToken)));
    }

    [HttpPost("{id:guid}/vista")]
    public async Task<ActionResult<OcorrenciaResponse>> Vista(Guid id, CancellationToken cancellationToken)
    {
        var o = await _ocorrencias.GetByIdAsync(id, cancellationToken);
        if (o is null || !Enxerga(o)) return NotFound();

        o.MarcarVista();
        await _ocorrencias.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(o, await RotulosAsync([o], cancellationToken)));
    }

    [HttpPost("{id:guid}/resolver")]
    public async Task<ActionResult<OcorrenciaResponse>> Resolver(
        Guid id, ResolverRequest request, CancellationToken cancellationToken)
    {
        var o = await _ocorrencias.GetByIdAsync(id, cancellationToken);
        if (o is null || !Enxerga(o)) return NotFound();

        // QUEM RESOLVEU vem do token, nao do corpo do pedido. Deixar o
        // cliente dizer quem foi e deixar o cliente assinar em nome de
        // outro.
        var quem = User.Identity?.Name ?? User.FindFirst("email")?.Value;

        o.Resolver(quem, request.Nota);
        await _ocorrencias.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(o, await RotulosAsync([o], cancellationToken)));
    }

    [HttpPost("{id:guid}/suporte")]
    public async Task<ActionResult<OcorrenciaResponse>> Suporte(
        Guid id, SuporteRequest request, CancellationToken cancellationToken)
    {
        var o = await _ocorrencias.GetByIdAsync(id, cancellationToken);
        if (o is null || !Enxerga(o)) return NotFound();

        o.EnviarAoSuporte(request.DescricaoDoAdmin);

        // O Admin CHAMA o suporte: daqui em diante ele é a outra ponta da conversa. Sem isto, o tecnico veria o erro na fila
        // e nao teria com quem falar — a ocorrencia que um detector achou nao tem dono, e o chat so abre quando ha alguem do
        // outro lado. Como dono, ela tambem passa a aparecer em "Suporte" no menu do Admin, onde ele le a resposta.
        if (User.IsInRole(Papeis.Admin) && Email() is { Length: > 0 } email)
        {
            if (string.IsNullOrWhiteSpace(o.AbertoPor))
                o.AbertoPorAlguem(email);

            // O que ele escreveu ao chamar vira a primeira fala da conversa, para o tecnico nao chegar sem contexto.
            if (!string.IsNullOrWhiteSpace(request.DescricaoDoAdmin)
                && string.Equals(o.AbertoPor, email, StringComparison.OrdinalIgnoreCase))
            {
                o.AdicionarMensagem(AutorDaMensagem.Admin, Nome(), email, request.DescricaoDoAdmin, DateTime.UtcNow);
            }
        }
        await _ocorrencias.SaveChangesAsync(cancellationToken);
        return Ok(ToResponse(o, await RotulosAsync([o], cancellationToken)));
    }

    [HttpGet("resumo")]
    public async Task<ActionResult<ResumoResponse>> Resumo(
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? ate,
        CancellationToken cancellationToken)
    {
        var fim = ate ?? DateTime.UtcNow;
        var ini = desde ?? fim.AddDays(-30);

        var linhas = await _ocorrencias.ResumoAsync(ini, fim, EmailSeForAdmin, cancellationToken);

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

    // ── quem esta do outro lado do token ─────────────────────────────────

    /// <summary>O técnico enxerga os dados de pessoas MASCARADOS; Admin e Criador, não.</summary>
    private bool Mascarar => User.IsInRole(Papeis.Suporte);

    private bool EhDaPlataforma => User.IsInRole(Papeis.Suporte) || User.IsInRole(Papeis.Criador);

    private string? Email() => User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email");

    private string Nome() => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue("name") ?? Email() ?? "sem nome";

    /// <summary>
    /// Para o Admin, o e-mail dele — que liga o filtro "só o que é meu". Para o suporte, nulo: enxerga tudo. Um Admin sem
    /// e-mail no token recebe um texto que não casa com ninguém: o filtro continua ligado, e ele não enxerga pedido nenhum
    /// (falha fechada), em vez de voltar a enxergar os dos compradores.
    /// </summary>
    private string? EmailSeForAdmin => User.IsInRole(Papeis.Admin) ? Email() ?? "sem-email" : null;

    /// <summary>
    /// O Admin enxerga o que os detectores acharam e os pedidos que ELE escreveu. O pedido de um comprador é do suporte:
    /// para o Admin, é como se não existisse (404, e não 403 — um 403 confirmaria que o id existe).
    /// </summary>
    private bool Enxerga(Ocorrencia o) => !User.IsInRole(Papeis.Admin) || !o.EhPedidoDeOutraPessoa(Email());

    /// <summary>
    /// Os rótulos ("0042 · Rede Sabor") das empresas de onde vieram estas ocorrências. Só para
    /// quem opera a plataforma: um Admin só enxerga a própria empresa, e o rótulo não lhe diz nada.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> RotulosAsync(
        IEnumerable<Ocorrencia> ocorrencias, CancellationToken cancellationToken)
    {
        if (!EhDaPlataforma) return new Dictionary<Guid, string>();

        var empresas = await _empresas.GetByIdsAsync(
            ocorrencias.Select(o => o.TenantId).OfType<Guid>(), cancellationToken);

        return empresas.ToDictionary(e => e.Id, e => e.Rotulo);
    }

    private OcorrenciaResponse ToResponse(Ocorrencia o, IReadOnlyDictionary<Guid, string> rotulos)
    {
        var mascarar = Mascarar;

        return new(
            o.Id,
            o.QuandoUtc,
            o.Sistema,
            o.Modulo,
            o.Operacao,
            o.Tipo.ToString(),
            o.Severidade.ToString(),
            o.Descricao,
            mascarar ? MascaraParaSuporte.DadosEnvolvidos(o.DadosEnvolvidosJson) : o.DadosEnvolvidosJson,
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
            o.UltimaVezUtc,
            mascarar ? MascaraParaSuporte.Email(o.AbertoPor) : o.AbertoPor,
            o.TenantId is { } t && rotulos.TryGetValue(t, out var rotulo) ? rotulo : null);
    }
}
