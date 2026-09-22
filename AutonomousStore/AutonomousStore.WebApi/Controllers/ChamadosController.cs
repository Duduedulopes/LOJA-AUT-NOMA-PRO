using System.Security.Claims;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Ocorrencias;
using AutonomousStore.WebApi.Services;
using AutonomousStore.WebApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using AutonomousStore.Domain.Common;

namespace AutonomousStore.WebApi.Controllers;

/// <summary>
/// Chamados: o que uma PESSOA escreveu para o suporte, e a conversa que veio
/// depois.
/// </summary>
/// <remarks>
/// POR QUE UM CONTROLADOR SEPARADO DO DE OCORRÊNCIAS.
///
/// Não é arrumação. O `OcorrenciasController` inteiro é
/// `[Authorize(Roles = "Admin,Suporte")]`, e atributo de autorização em
/// ASP.NET Core SOMA: um `[Authorize]` no método não afrouxa o da classe, os
/// dois precisam passar. Um cliente jamais entraria lá.
///
/// A saída seria `[AllowAnonymous]` no método e a checagem escrita à mão —
/// e aí a rota fica marcada como anônima no código, o que é a pior placa
/// possível numa rota que na verdade exige login. Um controlador com a regra
/// certa no topo diz a verdade sobre si mesmo.
///
/// QUEM VÊ O QUÊ. Admin e suporte veem qualquer chamado. Qualquer outra
/// pessoa vê só os que ela abriu. A regra mora na entidade
/// (`Ocorrencia.PodeConversar`) e não aqui: se cada rota repetisse a
/// condição, bastaria uma discordar para vazar.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChamadosController : ControllerBase
{
    private readonly IOcorrenciaRepository _ocorrencias;
    private readonly IRegistradorDeOcorrencia _registrador;
    private readonly IHubContext<ChamadoHub> _hub;
    private readonly ITenantRepository _empresas;

    public ChamadosController(
        IOcorrenciaRepository ocorrencias,
        IRegistradorDeOcorrencia registrador,
        IHubContext<ChamadoHub> hub,
        ITenantRepository empresas)
    {
        _ocorrencias = ocorrencias;
        _registrador = registrador;
        _hub = hub;
        _empresas = empresas;
    }

    /// <summary>Abre um chamado. A primeira mensagem já nasce dentro dele.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChamadoResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ChamadoResponse>> Abrir(
        AbrirChamadoRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Texto))
            return BadRequest(new { erro = "Escreva o que você precisa." });

        var email = Email();
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { erro = "Sua conta está sem e-mail — sem ele não há como te responder." });

        Ocorrencia chamado;
        try
        {
            chamado = Deteccoes.PedidoAoSuporte(
                app: AppDeQuemEsta(),
                ehMudanca: request.EhMudanca,
                assunto: request.Assunto,
                texto: request.Texto,
                quemNome: Nome(),
                quemEmail: email,
                paginaOndeEstava: request.Pagina,
                quandoUtc: DateTime.UtcNow);
        }
        catch (ArgumentException e)
        {
            return BadRequest(new { erro = e.Message });
        }

        // O tecnico e o Criador nao tem empresa. Um chamado PREVENTIVO ou INTERNO, aberto por eles,
        // e sobre uma empresa especifica: aqui eles dizem qual. Para Admin e comprador o campo e
        // ignorado — a empresa deles vem do token, e nao do corpo do pedido.
        if (EhDaPlataforma() && request.EmpresaId is { } empresaId)
        {
            var empresa = await _empresas.GetByIdAsync(empresaId, cancellationToken);
            if (empresa is null)
                return NotFound(new { erro = "Empresa não encontrada." });

            chamado.PertenceAoTenant(empresa.Id);
        }

        // Sem chave: pedido nunca é repetição de pedido. Duas pessoas
        // perguntando a mesma coisa são duas pessoas esperando resposta.
        var id = await _registrador.RegistrarAsync(chamado, cancellationToken);
        if (id is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { erro = "Não consegui gravar seu chamado agora. Tenta de novo em instantes." });

        var salvo = await _ocorrencias.ObterComConversaAsync(id.Value, cancellationToken) ?? chamado;
        return CreatedAtAction(
            nameof(Um), new { id = salvo.Id },
            ToResponse(salvo, comConversa: true, await RotulosAsync([salvo], cancellationToken)));
    }

    /// <summary>Os chamados de quem está pedindo — ou todos, se for da casa.</summary>
    [HttpGet("meus")]
    public async Task<ActionResult<IReadOnlyList<ChamadoResponse>>> Meus(
        CancellationToken cancellationToken)
    {
        var email = Email();
        if (string.IsNullOrWhiteSpace(email)) return Ok(Array.Empty<ChamadoResponse>());

        var lista = await _ocorrencias.ChamadosDeAsync(email, cancellationToken);

        // A lista NÃO traz a conversa: são cartões, e carregar a conversa de
        // vinte chamados para mostrar vinte títulos seria pagar caro por nada.
        var rotulos = await RotulosAsync(lista, cancellationToken);
        return Ok(lista.Select(o => ToResponse(o, comConversa: false, rotulos)).ToList());
    }

    /// <summary>Um chamado com a conversa inteira.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChamadoResponse>> Um(Guid id, CancellationToken cancellationToken)
    {
        var chamado = await _ocorrencias.ObterComConversaAsync(id, cancellationToken);
        if (chamado is null) return NotFound();

        // 404 e não 403 para quem não é dono: um 403 confirmaria que o
        // chamado existe, e o id é a única coisa que separa um chamado de
        // outro.
        if (!chamado.PodeConversar(Email(), EhDaCasa())) return NotFound();

        return Ok(ToResponse(chamado, comConversa: true, await RotulosAsync([chamado], cancellationToken)));
    }

    /// <summary>Responde dentro do chamado.</summary>
    [HttpPost("{id:guid}/mensagens")]
    public async Task<ActionResult<ChamadoResponse>> Responder(
        Guid id, ResponderRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Texto))
            return BadRequest(new { erro = "Mensagem vazia não é mensagem." });

        var chamado = await _ocorrencias.ObterComConversaAsync(id, cancellationToken);
        if (chamado is null) return NotFound();
        if (!chamado.PodeConversar(Email(), EhDaCasa())) return NotFound();

        chamado.AdicionarMensagem(
            autor: AutorDeQuemEsta(),
            quemNome: Nome(),
            quemEmail: Email(),
            texto: request.Texto,
            quandoUtc: DateTime.UtcNow);

        await _ocorrencias.SaveChangesAsync(cancellationToken);

        var resposta = ToResponse(chamado, comConversa: true, await RotulosAsync([chamado], cancellationToken));

        // ── O AVISO SAI DEPOIS DE GRAVAR, NUNCA ANTES ────────────────────
        //
        // Se o `SaveChanges` falhasse depois do aviso, as telas mostrariam uma
        // mensagem que não existe no banco — e ela sumiria no próximo F5, sem
        // nenhum rastro de que esteve lá. Avisar o que já é verdade custa uma
        // linha fora de ordem; avisar o que talvez seja custa confiança na
        // tela.
        //
        // QUEM FALOU TAMBÉM RECEBE, de propósito: quem escreveu já tem a
        // resposta da rota HTTP e o componente ignora o que é dele. Mandar
        // para o grupo inteiro deixa UM caminho para a mensagem aparecer, em
        // vez de dois que podem discordar.
        //
        // Sai o mesmo `ChamadoResponse` da rota HTTP: as telas não precisam
        // aprender um segundo formato, e nenhuma delas pode divergir do outro.
        //
        // O QUE VAI PARA O GRUPO E A VERSAO MASCARADA, SEMPRE, e sem o rotulo da empresa. O grupo
        // mistura quem pode ver o e-mail inteiro (o Admin) e quem nao pode (o tecnico): montar o
        // pacote para quem RESPONDEU e mandar para todos entregaria ao tecnico o e-mail que o
        // Admin, ao responder, tem direito de ver. A tela usa desta mensagem so a conversa; o
        // e-mail completo ela ja recebeu ao abrir o chamado, pela rota HTTP de cada um.
        var paraOGrupo = ToResponse(chamado, comConversa: true, rotulos: null, forcarMascara: true);

        await _hub.Clients.Group(ChamadoHub.Grupo(id))
            .SendAsync("MensagemNova", paraOGrupo, cancellationToken);

        return Ok(resposta);
    }

    // ── quem está do outro lado do token ─────────────────────────────────

    // "A CASA" E QUEM ATENDE: o tecnico e o Criador. O Admin NAO e da casa. Ele pede ajuda e e atendido, como o comprador:
    // so le e escreve nos chamados que ELE abriu. Os pedidos dos compradores vao para o suporte, e o Admin nao os ve.
    private bool EhDaCasa() => EhDaPlataforma();

    /// <remarks>
    /// Duas chaves porque o JwtBearer MAPEIA os nomes curtos do token para as
    /// URIs longas do WS-Federation quando `MapInboundClaims` está no padrão.
    /// Procurar só por uma das duas funciona até alguém mexer nessa opção — e
    /// aí o login continua passando e o e-mail some, que é o tipo de defeito
    /// que ninguém liga ao commit que o causou.
    /// </remarks>
    private string? Email()
        => User.FindFirstValue(ClaimTypes.Email)
           ?? User.FindFirstValue("email");

    private string Nome()
        => User.FindFirstValue(ClaimTypes.Name)
           ?? User.FindFirstValue("name")
           ?? Email()
           ?? "sem nome";

    /// <summary>De qual app veio — deduzido do papel, não do que o cliente diz.</summary>
    private string AppDeQuemEsta()
        => User.IsInRole(Papeis.Criador) ? "CriadorApp"
         : User.IsInRole(Papeis.Suporte) ? "SuporteApp"
         : User.IsInRole(Papeis.Admin) ? "AdminApp"
         : "ClientApp";

    private AutorDaMensagem AutorDeQuemEsta()
        // O Criador responde como Suporte: para quem pediu ajuda, quem responde e "a casa".
        => User.IsInRole(Papeis.Criador) || User.IsInRole(Papeis.Suporte) ? AutorDaMensagem.Suporte
         : User.IsInRole(Papeis.Admin) ? AutorDaMensagem.Admin
         : AutorDaMensagem.Cliente;

    // ── tradução ─────────────────────────────────────────────────────────

    /// <summary>Quem opera a plataforma: o tecnico e o Criador.</summary>
    private bool EhDaPlataforma() => User.IsInRole(Papeis.Suporte) || User.IsInRole(Papeis.Criador);

    /// <summary>O tecnico enxerga os dados de pessoas MASCARADOS; Admin e Criador, nao.</summary>
    private bool Mascarar => User.IsInRole(Papeis.Suporte);

    /// <summary>
    /// Os rotulos ("0042 · Rede Sabor") das empresas destes chamados. So para quem opera a
    /// plataforma: o Admin so ve a propria empresa, e o comprador nao precisa saber de qual e.
    /// </summary>
    private async Task<IReadOnlyDictionary<Guid, string>> RotulosAsync(
        IEnumerable<Ocorrencia> ocorrencias, CancellationToken cancellationToken)
    {
        if (!EhDaPlataforma()) return new Dictionary<Guid, string>();

        var empresas = await _empresas.GetByIdsAsync(
            ocorrencias.Select(o => o.TenantId).OfType<Guid>(), cancellationToken);

        return empresas.ToDictionary(e => e.Id, e => e.Rotulo);
    }

    // ── traducao ─────────────────────────────────────────────────────────

    private ChamadoResponse ToResponse(
        Ocorrencia o, bool comConversa, IReadOnlyDictionary<Guid, string>? rotulos, bool forcarMascara = false)
    {
        var conversa = o.Mensagens.OrderBy(m => m.QuandoUtc).ToList();
        var mascarar = forcarMascara || Mascarar;

        return new ChamadoResponse(
            o.Id,
            o.QuandoUtc,
            o.Sistema,
            o.Tipo.ToString(),
            o.Descricao,
            o.Estado.ToString(),
            mascarar ? MascaraParaSuporte.Email(o.AbertoPor) : o.AbertoPor,
            o.CorrelationId,
            conversa.Count > 0 ? conversa[^1].QuandoUtc : null,
            conversa.Count,
            comConversa
                ? conversa.Select(m => new MensagemResponse(
                        m.Id, m.QuandoUtc, m.Autor.ToString(), m.AutorNome, m.Texto)).ToList()
                : Array.Empty<MensagemResponse>(),
            o.TenantId is { } t && rotulos is not null && rotulos.TryGetValue(t, out var rotulo) ? rotulo : null);
    }
}
