using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Gerente.Models;

public record SessionItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal Subtotal);

public record SessionDto(
    Guid Id,
    Guid CustomerId,
    string QrCodeToken,
    DateTime QrCodeExpiresAt,
    SessionStatus Status,
    DateTime? EntryConfirmedAt,
    DateTime? ClosedAt,
    Guid? PaymentMethodId,
    DateTime? PaymentConfirmedAt,
    decimal Total,
    List<SessionItemDto> Items);