using AutonomousStore.Domain.Enums;

namespace AutonomousStore.WebApi.Contracts.Ocorrencias;

/// <summary>Abrir um chamado: uma dúvida ou um pedido de mudança.</summary>
/// <remarks>
/// O `App` NÃO vem daqui, e isso é de propósito: ele é deduzido do papel de
/// quem está no token. Se o cliente pudesse dizer "sou o AdminApp", a coluna
/// que o técnico usa para saber de onde veio o chamado passaria a ser
/// palpite.
/// </remarks>
public record AbrirChamadoRequest(
    string Assunto,
    string Texto,
    bool EhMudanca = false,
    string? Pagina = null,
    // Só vale para quem opera a plataforma (técnico e Criador): abrir um chamado preventivo
    // ou interno EM NOME de uma empresa. Para os demais e ignorado — a empresa deles vem do token.
    Guid? EmpresaId = null);

/// <summary>Uma resposta dentro de um chamado.</summary>
public record ResponderRequest(string Texto);

public record MensagemResponse(
    Guid Id,
    DateTime QuandoUtc,
    string Autor,
    string AutorNome,
    string Texto);

/// <summary>Um chamado como as telas o mostram.</summary>
public record ChamadoResponse(
    Guid Id,
    DateTime QuandoUtc,
    string Sistema,
    string Tipo,
    string Assunto,
    string Estado,
    string? AbertoPor,
    Guid CorrelationId,
    DateTime? UltimaMensagemUtc,
    int Mensagens,
    IReadOnlyList<MensagemResponse> Conversa,
    // A empresa de onde veio, para quem opera a plataforma (código + nome fantasia).
    string? Empresa = null);
