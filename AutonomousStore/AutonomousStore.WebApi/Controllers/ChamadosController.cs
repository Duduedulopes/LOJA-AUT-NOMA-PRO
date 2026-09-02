using System.Security.Claims;
using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Ocorrencias;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public ChamadosController(
        IOcorrenciaRepository ocorrencias, IRegistradorDeOcorrencia registrador)
    {
        _ocorrencias = ocorrencias;
        _registrador = registrador;
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

        // Sem chave: pedido nunca é repetição de pedido. Duas pessoas
        // perguntando a mesma coisa são duas pessoas esperando resposta.
        var id = await _registrador.RegistrarAsync(chamado, cancellationToken);
        if (id is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { erro = "Não consegui gravar seu chamado agora. Tenta de novo em instantes." });

        var salvo = await _ocorrencias.ObterComConversaAsync(id.Value, cancellationToken) ?? chamado;
        return CreatedAtAction(nameof(Um), new { id = salvo.Id }, ToResponse(salvo));
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
        return Ok(lista.Select(o => ToResponse(o, comConversa: false)).ToList());
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

        return Ok(ToResponse(chamado));
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
        return Ok(ToResponse(chamado));
    }

    // ── quem está do outro lado do token ─────────────────────────────────

    private bool EhDaCasa() => User.IsInRole("Admin") || User.IsInRole("Suporte");

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
        => User.IsInRole("Suporte") ? "SuporteApp"
         : User.IsInRole("Admin") ? "AdminApp"
         : "ClientApp";

    private AutorDaMensagem AutorDeQuemEsta()
        => User.IsInRole("Suporte") ? AutorDaMensagem.Suporte
         : User.IsInRole("Admin") ? AutorDaMensagem.Admin
         : AutorDaMensagem.Cliente;

    // ── tradução ─────────────────────────────────────────────────────────

    private static ChamadoResponse ToResponse(Ocorrencia o, bool comConversa = true)
    {
        var conversa = o.Mensagens.OrderBy(m => m.QuandoUtc).ToList();

        return new ChamadoResponse(
            o.Id,
            o.QuandoUtc,
            o.Sistema,
            o.Tipo.ToString(),
            o.Descricao,
            o.Estado.ToString(),
            o.AbertoPor,
            o.CorrelationId,
            conversa.Count > 0 ? conversa[^1].QuandoUtc : null,
            conversa.Count,
            comConversa
                ? conversa.Select(m => new MensagemResponse(
                        m.Id, m.QuandoUtc, m.Autor.ToString(), m.AutorNome, m.Texto)).ToList()
                : Array.Empty<MensagemResponse>());
    }
}
