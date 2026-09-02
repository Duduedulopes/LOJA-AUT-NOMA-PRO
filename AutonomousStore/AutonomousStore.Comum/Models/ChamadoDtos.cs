using System.Text.Json.Serialization;

namespace AutonomousStore.Comum.Models;

/// <summary>Uma fala dentro de um chamado.</summary>
public record MensagemDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("quandoUtc")] DateTime QuandoUtc,

    /// <summary>"Cliente", "Admin" ou "Suporte" — texto, e não número.</summary>
    /// <remarks>
    /// String de propósito, igual ao resto dos DTOs deste sistema: se amanhã
    /// nascer um autor novo na WebApi, este lado NÃO quebra. Um enum tipado
    /// aqui estouraria na desserialização com um valor que ele não conhece, e
    /// a conversa inteira sumiria da tela por causa de uma categoria nova.
    /// </remarks>
    [property: JsonPropertyName("autor")] string Autor,
    [property: JsonPropertyName("autorNome")] string AutorNome,
    [property: JsonPropertyName("texto")] string Texto);

/// <summary>Um chamado: o assunto, o estado e a conversa.</summary>
public record ChamadoDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("quandoUtc")] DateTime QuandoUtc,
    [property: JsonPropertyName("sistema")] string Sistema,
    [property: JsonPropertyName("tipo")] string Tipo,
    [property: JsonPropertyName("assunto")] string Assunto,
    [property: JsonPropertyName("estado")] string Estado,
    [property: JsonPropertyName("abertoPor")] string? AbertoPor,
    [property: JsonPropertyName("correlationId")] Guid CorrelationId,
    [property: JsonPropertyName("ultimaMensagemUtc")] DateTime? UltimaMensagemUtc,
    [property: JsonPropertyName("mensagens")] int Mensagens,
    [property: JsonPropertyName("conversa")] IReadOnlyList<MensagemDto> Conversa);

/// <summary>O que a tela manda para abrir um chamado.</summary>
public record AbrirChamadoDto(
    [property: JsonPropertyName("assunto")] string Assunto,
    [property: JsonPropertyName("texto")] string Texto,
    [property: JsonPropertyName("ehMudanca")] bool EhMudanca,
    [property: JsonPropertyName("pagina")] string? Pagina);
