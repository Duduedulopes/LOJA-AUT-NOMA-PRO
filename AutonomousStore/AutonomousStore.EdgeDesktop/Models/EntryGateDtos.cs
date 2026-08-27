namespace AutonomousStore.EdgeDesktop.Models;

/// <summary>Request do endpoint POST /api/sessions/confirm-entry.</summary>
public record ConfirmEntryRequest(string QrCodeToken);

/// <summary>Resposta do endpoint POST /api/sessions/confirm-entry.</summary>
public record ConfirmEntryResponse(
    bool Allowed,
    string? CustomerName,
    Guid? SessionId,
    string Message,
    DateTime? EntryConfirmedAt);

/// <summary>Resultado já tratado para a UI (inclui erros HTTP como mensagens).</summary>
public record ConfirmEntryResult(
    bool Allowed,
    string Message,
    string? CustomerName = null,
    Guid? SessionId = null,
    DateTime? EntryConfirmedAt = null);

