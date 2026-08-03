using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

public interface IStoreSessionRepository
{
    Task AddAsync(StoreSession session, CancellationToken cancellationToken = default);
    Task<StoreSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<StoreSession?> GetByQrCodeTokenAsync(string qrCodeToken, CancellationToken cancellationToken = default);

    /// <summary>Busca a sessão em andamento (aguardando entrada ou aberta) de um cliente, se existir.</summary>
    Task<StoreSession?> GetActiveSessionByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca a sessão com status "Aberta" no momento — usada pelo módulo de Hardware (câmera/RFID),
    /// que não sabe de qual cliente é a sessão, só que alguém está dentro da loja comprando.
    /// Simplificação do protótipo: assume um cliente ativo por vez na loja. Com mais de uma sessão
    /// aberta simultânea, seria preciso identificar o cliente por outro meio (ex: proximidade do
    /// celular) — isso é uma evolução futura, ainda não suportada.
    /// </summary>
    Task<StoreSession?> GetCurrentOpenSessionAsync(CancellationToken cancellationToken = default);

    /// <summary>Lista as sessões que já geraram QR code mas ainda aguardam a confirmação de entrada.</summary>
    Task<IReadOnlyList<StoreSession>> GetPendingEntrySessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Histórico de vendas: sessões concluídas (pagamento confirmado), mais recentes primeiro.
    /// Usado pelo painel admin pra listar o que já foi vendido.
    /// </summary>
    Task<IReadOnlyList<StoreSession>> GetHistoryAsync(CancellationToken cancellationToken = default);

    /// <summary>Histórico de compras concluídas de um cliente específico — usado na tela "Minhas compras" do ClientApp.</summary>
    Task<IReadOnlyList<StoreSession>> GetHistoryByCustomerAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Busca a sessão mais recente do cliente que está na loja agora (não cancelada), não importa
    /// o status exato — usada na verificação de saída, pra conferir se o que ele está levando foi pago.
    /// </summary>
    Task<StoreSession?> GetMostRecentSessionAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
