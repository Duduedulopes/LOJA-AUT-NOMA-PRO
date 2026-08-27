using AutonomousStore.EdgeDesktop.Models;

namespace AutonomousStore.EdgeDesktop.Services;

public interface ISessionApiService
{
    Task<SessionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SessionDto> AddItemByRfidAsync(Guid id, string rfidTag, CancellationToken cancellationToken = default);
    Task<ConfirmEntryResult> ConfirmEntryAsync(string qrCodeToken, CancellationToken cancellationToken = default);
}
