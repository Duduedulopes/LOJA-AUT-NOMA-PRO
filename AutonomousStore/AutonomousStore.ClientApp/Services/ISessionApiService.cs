using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

public interface ISessionApiService
{
    Task<(bool Success, SessionDto? Session, string? Error)> CreateAsync(Guid customerId);
    Task<SessionDto?> GetActiveAsync(Guid customerId);
    Task<(bool Success, SessionDto? Session, string? Error)> RegenerateQrCodeAsync(Guid id);
    Task<SessionDto?> GetByIdAsync(Guid id);
    Task<(bool Success, string? Error)> ConfirmEntryAsync(string qrCodeToken);
    Task<(bool Success, SessionDto? Session, string? Error)> CheckoutAsync(Guid id);
    Task<(bool Success, SessionDto? Session, string? Error)> ConfirmPaymentAsync(Guid id, Guid paymentMethodId);
    Task<List<SessionDto>> GetHistoryAsync(Guid customerId);
    Task<(bool Success, string? Error)> CancelAsync(Guid id);
}
