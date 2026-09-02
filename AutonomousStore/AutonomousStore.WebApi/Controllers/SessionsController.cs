using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Enums;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Sessions;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SessionsController : ControllerBase
{
    private readonly IStoreSessionRepository _sessionRepository;
    private readonly ICustomerRepository _customerRepository;
    private readonly IProductRepository _productRepository;
    private readonly IRegistradorDeOcorrencia _ocorrencias;

    public SessionsController(
        IStoreSessionRepository sessionRepository,
        ICustomerRepository customerRepository,
        IProductRepository productRepository,
        IRegistradorDeOcorrencia ocorrencias)
    {
        _sessionRepository = sessionRepository;
        _customerRepository = customerRepository;
        _productRepository = productRepository;
        _ocorrencias = ocorrencias;
    }

    /// <summary>Gera uma nova sessão (visita) e o QR code que abre a porta da loja.</summary>
    [HttpPost]
    public async Task<ActionResult<SessionResponse>> Create(
        CreateSessionRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(request.CustomerId, cancellationToken);

        if (customer is null)
            return NotFound(new { error = "Cliente não encontrado." });

        var activeSession = await _sessionRepository.GetActiveSessionByCustomerAsync(customer.Id, cancellationToken);

        if (activeSession is not null)
        {
            // Sessão abandonada (QR nunca lido e vencido, ou visita que nunca fechou) não pode
            // bloquear o cliente pra sempre — cancela e libera uma nova.
            if (activeSession.TryExpire(DateTime.UtcNow))
            {
                await _sessionRepository.SaveChangesAsync(cancellationToken);
            }
            else
            {
                return Conflict(new { error = "Já existe uma sessão em andamento para este cliente." });
            }
        }

        var session = new StoreSession(customer.Id);

        await _sessionRepository.AddAsync(session, cancellationToken);
        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = session.Id }, ToResponse(session));
    }

    /// <summary>Busca a sessão em andamento (aguardando entrada ou aberta) do cliente, se houver.</summary>
    /// <remarks>
    /// SÓ O DONO DA SESSÃO, OU A CASA.
    ///
    /// A rota exigia estar autenticado e nada mais: qualquer cliente logado
    /// podia trocar o GUID da URL pelo de outro e ler o carrinho dele — os
    /// produtos, as quantidades e o total. Estar logado provava que ele é
    /// alguém, nunca que ele é ESTE alguém.
    ///
    /// Apareceu quando o gerente virtual passou a responder "o que tem no meu
    /// carrinho?" no app do cliente e precisou desta rota. A falha já existia
    /// antes disso, esperando alguém chamar.
    /// </remarks>
    [HttpGet("active/{customerId:guid}")]
    public async Task<ActionResult<SessionResponse>> GetActive(Guid customerId, CancellationToken cancellationToken)
    {
        if (!PodeVerSessaoDe(customerId))
            return Forbid();

        var session = await _sessionRepository.GetActiveSessionByCustomerAsync(customerId, cancellationToken);

        if (session is null)
            return NotFound();

        // Se a sessão ficou pra trás, ela é encerrada aqui e o cliente vê a tela de gerar QR code
        // em vez de um "você já está dentro da loja" herdado de uma visita que nunca terminou.
        if (session.TryExpire(DateTime.UtcNow))
        {
            await _sessionRepository.SaveChangesAsync(cancellationToken);
            return NotFound();
        }

        return Ok(ToResponse(session));
    }

    /// <summary>
    /// Busca a sessão "Aberta" no momento, sem precisar saber de qual cliente é — usada pelo
    /// módulo de Hardware (câmera de prateleira), que só sabe que alguém está comprando.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("current-open")]
    public async Task<ActionResult<SessionResponse>> GetCurrentOpen(CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetCurrentOpenSessionAsync(cancellationToken);

        if (session is null)
            return NotFound(new { error = "Nenhuma sessão aberta no momento." });

        return Ok(ToResponse(session));
    }

    /// <summary>Lista as sessões que geraram QR code mas ainda não confirmaram entrada — usado pelo painel admin.</summary>
    [AllowAnonymous]
    [HttpGet("pending-entry")]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> GetPendingEntry(CancellationToken cancellationToken)
    {
        var sessions = await _sessionRepository.GetPendingEntrySessionsAsync(cancellationToken);
        return Ok(sessions.Select(ToResponse).ToList());
    }

    /// <summary>Histórico de vendas (sessões concluídas) — usado pela tela de histórico do painel admin.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> GetHistory(CancellationToken cancellationToken)
    {
        var sessions = await _sessionRepository.GetHistoryAsync(cancellationToken);
        return Ok(sessions.Select(ToResponse).ToList());
    }

    /// <summary>Histórico de compras concluídas de um cliente — usado na tela "Minhas compras" do ClientApp.</summary>
    [HttpGet("history/{customerId:guid}")]
    public async Task<ActionResult<IReadOnlyList<SessionResponse>>> GetHistoryByCustomer(Guid customerId, CancellationToken cancellationToken)
    {
        var sessions = await _sessionRepository.GetHistoryByCustomerAsync(customerId, cancellationToken);
        return Ok(sessions.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SessionResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(id, cancellationToken);

        if (session is null)
            return NotFound();

        return Ok(ToResponse(session));
    }

    /// <summary>Usado pela leitora da porta: valida o QR code exibido no app e libera a entrada.</summary>
    /// <remarks>Simplificação do protótipo: sem autenticação própria de hardware ainda — isso deve
    /// evoluir para uma chave de API do dispositivo quando o módulo de Hardware for implementado.</remarks>
    [AllowAnonymous]
    [HttpGet("by-qrcode/{qrCodeToken}")]
    public async Task<ActionResult<SessionResponse>> GetByQrCodeToken(string qrCodeToken, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByQrCodeTokenAsync(qrCodeToken, cancellationToken);

        if (session is null)
            return NotFound();

        return Ok(ToResponse(session));
    }

    /// <summary>
    /// Usado pela leitora da porta: recebe o conteúdo lido do QR code, valida e libera a entrada.
    /// Propositalmente NÃO aceita Id de sessão — saber o Id não pode ser suficiente para abrir a
    /// porta; é preciso ter lido o QR que está na tela do cliente, e ele expira em
    /// <see cref="StoreSession.QrCodeValidityMinutes"/> minutos.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("confirm-entry")]
    public async Task<ActionResult<ConfirmEntryResponse>> ConfirmEntry(
        ConfirmEntryRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.QrCodeToken))
            return BadRequest(new { error = "QR code vazio." });

        var session = await _sessionRepository.GetByQrCodeTokenAsync(request.QrCodeToken, cancellationToken);

        if (session is null)
            return NotFound(new { error = "QR code não corresponde a nenhuma sessão." });

        try
        {
            session.ConfirmEntry(request.QrCodeToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        var customer = await _customerRepository.GetByIdAsync(session.CustomerId, cancellationToken);
        var customerName = customer?.Name ?? "Cliente";

        return Ok(new ConfirmEntryResponse(
            Allowed: true,
            CustomerName: customerName,
            SessionId: session.Id,
            Message: "Entrada liberada.",
            EntryConfirmedAt: session.EntryConfirmedAt));
    }

    /// <summary>Usado pelo módulo de Hardware quando um produto é identificado (RFID/sensor).</summary>
    [AllowAnonymous]
    [HttpPost("{id:guid}/items")]
    public async Task<ActionResult<SessionResponse>> AddItem(
        Guid id,
        AddSessionItemRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(id, cancellationToken);

        if (session is null)
            return NotFound();

        try
        {
            session.AddItem(request.ProductId, request.ProductName, request.UnitPrice, request.Quantity);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _sessionRepository.SaveChangesAsync(cancellationToken);
        await _productRepository.DecreaseStockAsync(request.ProductId, request.Quantity, cancellationToken);

        return Ok(ToResponse(session));
    }

    /// <summary>
    /// Igual ao endpoint acima, mas usado pelo leitor RFID: recebe só a tag lida, resolve
    /// pra qual produto ela pertence, e adiciona esse produto na sessão.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{id:guid}/items/by-rfid")]
    public async Task<ActionResult<SessionResponse>> AddItemByRfid(
        Guid id,
        AddSessionItemByRfidRequest request,
        CancellationToken cancellationToken)
    {
        // Log de apoio ao desenvolvimento do leitor de saída: o firmware do ESP32 não tem
        // saída de texto utilizável nesta máquina, então a tag lida aparece aqui no console
        // da API. É por aqui que se descobre o UID de um cartão novo para cadastrar no
        // AdminApp. Pode sair quando o leitor estiver estável.
        Console.WriteLine($"[RFID] tag recebida: \"{request.RfidTag}\"  (sessao {id})");

        var session = await _sessionRepository.GetByIdAsync(id, cancellationToken);

        if (session is null)
            return NotFound(new { error = "Sessão não encontrada." });

        var product = await _productRepository.GetByRfidTagAsync(request.RfidTag, cancellationToken);

        if (product is null)
        {
            Console.WriteLine($"[RFID] nenhum produto com essa tag. Cadastre \"{request.RfidTag}\" no AdminApp.");
            return NotFound(new { error = $"Nenhum produto vinculado à tag \"{request.RfidTag}\"." });
        }

        Console.WriteLine($"[RFID] produto: {product.Name} — R$ {product.Price}");

        try
        {
            session.AddItem(product.Id, product.Name, product.Price);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _sessionRepository.SaveChangesAsync(cancellationToken);
        await _productRepository.DecreaseStockAsync(product.Id, 1, cancellationToken);

        return Ok(ToResponse(session));
    }

    [HttpDelete("{id:guid}/items/{productId:guid}")]
    public async Task<ActionResult<SessionResponse>> RemoveItem(
        Guid id,
        Guid productId,
        [FromQuery] int quantity,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(id, cancellationToken);

        if (session is null)
            return NotFound();

        var quantityToRemove = quantity <= 0 ? 1 : quantity;

        try
        {
            session.RemoveItem(productId, quantityToRemove);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _sessionRepository.SaveChangesAsync(cancellationToken);
        await _productRepository.IncreaseStockAsync(productId, quantityToRemove, cancellationToken);

        return Ok(ToResponse(session));
    }

    /// <summary>Chamado quando o cliente sai da loja: fecha a sessão e trava o valor total.</summary>
    [HttpPost("{id:guid}/checkout")]
    public async Task<ActionResult<SessionResponse>> Checkout(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(id, cancellationToken);

        if (session is null)
            return NotFound();

        try
        {
            session.RequestCheckout();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(session));
    }

    /// <summary>Chamado quando o cliente confirma o pagamento no app, depois de ver o total.</summary>
    [HttpPost("{id:guid}/confirm-payment")]
    public async Task<ActionResult<SessionResponse>> ConfirmPayment(
        Guid id,
        ConfirmPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(id, cancellationToken);

        if (session is null)
            return NotFound();

        try
        {
            session.ConfirmPayment(request.PaymentMethodId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(session));
    }

    /// <summary>Cancela a sessão e DEVOLVE ao estoque o que estava no carrinho.</summary>
    /// <remarks>
    /// O FURO QUE ESTAVA AQUI. O estoque baixa no `AddItem` — o produto saiu
    /// fisicamente da prateleira. O `RemoveItem` devolve, com
    /// `IncreaseStockAsync`. Este método NÃO devolvia.
    ///
    /// O resultado é silencioso e permanente: sessão cancelada com carrinho
    /// cheio deixava o sistema contando a menos do que existe. O produto está
    /// na prateleira e o catálogo jura que saiu. Ninguém recebe erro, ninguém
    /// vê log, e a diferença só aparece numa contagem física — que quase nunca
    /// acontece.
    ///
    /// A DEVOLUÇÃO VEM ANTES DO `SaveChanges` DA SESSÃO, de propósito: se
    /// devolver estoque falhar, a sessão não é marcada como cancelada, e o
    /// próximo cancelamento tenta tudo de novo. Na ordem inversa, uma falha
    /// no meio deixaria a sessão cancelada e o estoque no chão — exatamente o
    /// estado que este método existe para evitar.
    /// </remarks>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(id, cancellationToken);

        if (session is null)
            return NotFound();

        // Fotografa o carrinho ANTES de cancelar: depois do `Cancel` o estado
        // muda, e o que se precisa devolver é o que estava lá agora.
        var devolver = session.Items
            .Select(i => (i.ProductId, i.ProductName, i.Quantity, i.Subtotal))
            .ToList();

        try
        {
            session.Cancel();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        foreach (var item in devolver)
            await _productRepository.IncreaseStockAsync(item.ProductId, item.Quantity, cancellationToken);

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        if (devolver.Count > 0)
        {
            // Registrado mesmo dando certo. Cancelamento com carrinho cheio é
            // movimento de estoque que não passou por venda nenhuma — o Chefe
            // tem direito de ver isso no histórico, ainda que sem estrago.
            var o = Deteccoes.SessaoCanceladaComItem(
                session.Id,
                DateTime.UtcNow,
                devolver.Select(d => (d.ProductName, d.Quantity, d.Subtotal)).ToList());

            o.RegistrarAcao(
                $"Devolvidas {devolver.Sum(d => d.Quantity)} unidade(s) ao estoque no cancelamento.",
                "Estoque conferido — o cancelamento não deixou buraco.");
            o.Resolver(quem: "sistema", nota: null,
                       resultado: "Corrigido na origem: o Cancel agora devolve.");

            await _ocorrencias.RegistrarAsync(o, cancellationToken);
        }

        return NoContent();
    }

    /// <summary>Gera um novo QR code (e nova validade) quando o anterior expirou.</summary>
    [HttpPost("{id:guid}/regenerate-qrcode")]
    public async Task<ActionResult<SessionResponse>> RegenerateQrCode(Guid id, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(id, cancellationToken);

        if (session is null)
            return NotFound();

        try
        {
            session.RegenerateQrCode();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _sessionRepository.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(session));
    }

    /// <summary>
    /// Verificação de saída (antifurto): o leitor da porta lê a tag de um produto que o cliente
    /// está carregando ao sair, e essa checagem confere se esse produto foi pago. Não recebe Id de
    /// sessão — assim como a câmera de prateleira, resolve sozinho qual é a sessão mais recente.
    /// </summary>
    /// <remarks>
    /// O ALARME AGORA FICA GRAVADO.
    ///
    /// Antes, cada um dos três caminhos de ALARME devolvia a mensagem para a
    /// leitora e acabava ali. A tela da porta acendia vermelho, alguém via ou
    /// não via, e no dia seguinte não havia como responder "tivemos algum
    /// furo ontem?" — porque não havia onde olhar. Um antifurto que não deixa
    /// registro é uma campainha, não um sistema.
    ///
    /// A GRAVAÇÃO NUNCA ATRAPALHA A PORTA. O `IRegistradorDeOcorrencia`
    /// engole a própria falha e devolve `null`: se o banco estiver fora, a
    /// leitora ainda recebe a resposta em tempo. O remédio não pode matar o
    /// paciente.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("verify-exit")]
    public async Task<ActionResult<VerifyExitResponse>> VerifyExit(VerifyExitRequest request, CancellationToken cancellationToken)
    {
        var agora = DateTime.UtcNow;
        var product = await _productRepository.GetByRfidTagAsync(request.RfidTag, cancellationToken);

        if (product is null)
        {
            // Tag desconhecida saindo pela porta — trata como suspeito por padrão, nunca libera às cegas.
            await _ocorrencias.RegistrarAsync(
                Deteccoes.TagDesconhecidaNaPorta(request.RfidTag, agora), cancellationToken);

            return Ok(new VerifyExitResponse(false, null, $"Tag \"{request.RfidTag}\" não corresponde a nenhum produto conhecido."));
        }

        var session = await _sessionRepository.GetMostRecentSessionAsync(cancellationToken);

        if (session is null)
        {
            await _ocorrencias.RegistrarAsync(
                Deteccoes.SaidaSemPagamento(request.RfidTag, product.Name, null, "nenhuma sessão", agora),
                cancellationToken);

            return Ok(new VerifyExitResponse(false, product.Name, "Nenhuma sessão de compra encontrada para esse produto."));
        }

        var itemInSession = session.Items.FirstOrDefault(i => i.ProductId == product.Id);

        if (itemInSession is null)
        {
            await _ocorrencias.RegistrarAsync(
                Deteccoes.SaidaSemPagamento(request.RfidTag, product.Name, session.Id,
                                            "produto não está no carrinho", agora),
                cancellationToken);

            return Ok(new VerifyExitResponse(false, product.Name, $"{product.Name} não foi registrado em nenhuma compra — ALARME."));
        }

        var isPaid = session.Status == SessionStatus.Concluida;

        if (!isPaid)
        {
            await _ocorrencias.RegistrarAsync(
                Deteccoes.SaidaSemPagamento(request.RfidTag, product.Name, session.Id,
                                            session.Status.ToString(), agora),
                cancellationToken);
        }

        var message = isPaid
            ? $"{product.Name} — pagamento confirmado, tudo certo."
            : $"{product.Name} foi escaneado, mas o pagamento ainda não foi confirmado — ALARME.";

        return Ok(new VerifyExitResponse(isPaid, product.Name, message));
    }

    /// <summary>Ou é a sessão dele, ou quem pergunta é do painel.</summary>
    /// <remarks>
    /// O `sub` do token é o Id de quem entrou. Com o mapeamento padrão do
    /// ASP.NET Core ele chega como `NameIdentifier`; o `sub` cru fica de
    /// reserva para o caso de alguém desligar esse mapeamento um dia.
    /// </remarks>
    private bool PodeVerSessaoDe(Guid customerId)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Suporte")) return true;

        var meu = User.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? User.FindFirstValue("sub");

        return Guid.TryParse(meu, out var eu) && eu == customerId;
    }

    private static SessionResponse ToResponse(StoreSession session) => new(
        session.Id,
        session.CustomerId,
        session.QrCodeToken,
        session.QrCodeExpiresAt,
        session.Status,
        session.EntryConfirmedAt,
        session.ClosedAt,
        session.PaymentMethodId,
        session.PaymentConfirmedAt,
        session.Total,
        session.Items
            .Select(i => new SessionItemResponse(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.Subtotal))
            .ToList());
}
