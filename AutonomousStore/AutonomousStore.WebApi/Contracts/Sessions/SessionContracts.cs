using AutonomousStore.Domain.Enums;

namespace AutonomousStore.WebApi.Contracts.Sessions;

public record CreateSessionRequest(Guid CustomerId);

public record AddSessionItemRequest(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity = 1);

public record AddSessionItemByRfidRequest(string RfidTag);

public record VerifyExitRequest(string RfidTag);

public record VerifyExitResponse(bool IsPaid, string? ProductName, string Message);

public record ConfirmPaymentRequest(Guid PaymentMethodId);

/// <summary>O que a leitora da porta envia: só o conteúdo lido do QR code.</summary>
public record ConfirmEntryRequest(string QrCodeToken);

/// <summary>Resposta específica da leitora da porta: pensada para exibir em uma tela grande (verde/vermelho).</summary>
public record ConfirmEntryResponse(
    bool Allowed,
    string? CustomerName,
    Guid? SessionId,
    string Message,
    DateTime? EntryConfirmedAt);

public record SessionItemResponse(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    int Quantity,
    decimal Subtotal);

public record SessionResponse(
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
    IReadOnlyList<SessionItemResponse> Items);
