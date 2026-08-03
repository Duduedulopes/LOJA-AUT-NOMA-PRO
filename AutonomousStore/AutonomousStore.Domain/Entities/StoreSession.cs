using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Entities;

/// <summary>
/// Representa uma visita do cliente à loja: geração do QR code que abre a porta,
/// os itens escaneados durante a compra, o fechamento (checkout) e a confirmação do pagamento.
/// </summary>
public class StoreSession : Entity
{
    private readonly List<SessionItem> _items = [];

    public Guid CustomerId { get; private set; }
    public string QrCodeToken { get; private set; }
    public DateTime QrCodeExpiresAt { get; private set; }
    public SessionStatus Status { get; private set; }
    public DateTime? EntryConfirmedAt { get; private set; }
    public DateTime? ClosedAt { get; private set; }
    public Guid? PaymentMethodId { get; private set; }
    public DateTime? PaymentConfirmedAt { get; private set; }

    public IReadOnlyList<SessionItem> Items => _items.AsReadOnly();
    public decimal Total => _items.Sum(i => i.Subtotal);

    protected StoreSession() { }

    public const int QrCodeValidityMinutes = 5;

    /// <summary>
    /// Tempo máximo de uma visita. Passado isso sem checkout, a sessão é considerada abandonada
    /// (o cliente foi embora, o teste travou, o navegador fechou) e deixa de bloquear o cliente.
    /// </summary>
    public const int AbandonedVisitMinutes = 60;

    public StoreSession(Guid customerId)
    {
        CustomerId = customerId;
        QrCodeToken = Guid.NewGuid().ToString("N");
        QrCodeExpiresAt = DateTime.UtcNow.AddMinutes(QrCodeValidityMinutes);
        Status = SessionStatus.AguardandoEntrada;
    }

    /// <summary>
    /// Chamado quando a leitora da porta valida o QR code e libera a entrada. Exige o token lido
    /// do QR: quem não leu o código do cliente não consegue abrir a porta, mesmo sabendo o Id da sessão.
    /// </summary>
    public void ConfirmEntry(string qrCodeToken)
    {
        if (Status != SessionStatus.AguardandoEntrada)
            throw new InvalidOperationException("A sessão não está aguardando entrada.");

        if (!string.Equals(QrCodeToken, qrCodeToken, StringComparison.Ordinal))
            throw new InvalidOperationException("QR code inválido para esta sessão.");

        if (DateTime.UtcNow > QrCodeExpiresAt)
            throw new InvalidOperationException("O QR code expirou. Gere um novo para tentar novamente.");

        Status = SessionStatus.Aberta;
        EntryConfirmedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cancela a sessão se ela ficou pra trás: QR gerado e nunca lido depois da validade, ou entrada
    /// confirmada e nunca fechada. Retorna true se cancelou. Sem isso, uma sessão travada segue sendo
    /// "ativa" pra sempre e o cliente nunca mais consegue gerar um QR code novo.
    /// </summary>
    public bool TryExpire(DateTime utcNow)
    {
        if (Status == SessionStatus.AguardandoEntrada && utcNow > QrCodeExpiresAt)
        {
            Cancel();
            return true;
        }

        // EntryConfirmedAt sempre existe numa sessão aberta, mas caímos em CreatedAt por segurança:
        // uma sessão sem data nenhuma nunca expiraria e voltaria a travar o cliente.
        if (Status == SessionStatus.Aberta
            && utcNow > (EntryConfirmedAt ?? CreatedAt).AddMinutes(AbandonedVisitMinutes))
        {
            Cancel();
            return true;
        }

        return false;
    }

    /// <summary>Gera um novo QR code (e nova validade) quando o anterior expirou, sem precisar criar outra sessão.</summary>
    public void RegenerateQrCode()
    {
        if (Status != SessionStatus.AguardandoEntrada)
            throw new InvalidOperationException("Só é possível gerar um novo QR code enquanto aguarda entrada.");

        QrCodeToken = Guid.NewGuid().ToString("N");
        QrCodeExpiresAt = DateTime.UtcNow.AddMinutes(QrCodeValidityMinutes);
    }

    /// <summary>Chamado pelo módulo de Hardware (RFID/sensores) quando um produto é identificado.</summary>
    public void AddItem(Guid productId, string productName, decimal unitPrice, int quantity = 1)
    {
        if (Status != SessionStatus.Aberta)
            throw new InvalidOperationException("Só é possível adicionar itens em uma sessão aberta.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);

        if (existing is not null)
        {
            existing.IncreaseQuantity(quantity);
            return;
        }

        _items.Add(new SessionItem(Id, productId, productName, unitPrice, quantity));
    }

    public void RemoveItem(Guid productId, int quantity = 1)
    {
        if (Status != SessionStatus.Aberta)
            throw new InvalidOperationException("Só é possível remover itens em uma sessão aberta.");

        var existing = _items.FirstOrDefault(i => i.ProductId == productId);

        if (existing is null)
            return;

        existing.DecreaseQuantity(quantity);

        if (existing.Quantity <= 0)
            _items.Remove(existing);
    }

    /// <summary>Chamado quando o cliente sai da loja: fecha a sessão e trava o valor total.</summary>
    public void RequestCheckout()
    {
        if (Status != SessionStatus.Aberta)
            throw new InvalidOperationException("A sessão precisa estar aberta para finalizar a compra.");

        if (_items.Count == 0)
            throw new InvalidOperationException("Não é possível finalizar uma sessão sem itens.");

        Status = SessionStatus.AguardandoPagamento;
        ClosedAt = DateTime.UtcNow;
    }

    /// <summary>Chamado quando o cliente confirma o pagamento no app, depois de ver o total.</summary>
    public void ConfirmPayment(Guid paymentMethodId)
    {
        if (Status != SessionStatus.AguardandoPagamento)
            throw new InvalidOperationException("A sessão não está aguardando pagamento.");

        PaymentMethodId = paymentMethodId;
        PaymentConfirmedAt = DateTime.UtcNow;
        Status = SessionStatus.Concluida;
    }

    public void Cancel()
    {
        if (Status is SessionStatus.Concluida or SessionStatus.Cancelada)
            throw new InvalidOperationException("Não é possível cancelar uma sessão já concluída ou cancelada.");

        Status = SessionStatus.Cancelada;
    }
}