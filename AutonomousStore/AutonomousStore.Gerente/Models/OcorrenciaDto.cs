using System.Text.Json.Serialization;

namespace AutonomousStore.Gerente.Models;

/// <summary>
/// Uma ocorrencia como a WebApi devolve.
/// </summary>
/// <remarks>
/// OS ENUMS CHEGAM COMO TEXTO — "Roubo", nao 9. A WebApi liga o
/// `JsonStringEnumConverter`, e aqui eles ficam como `string` de proposito:
/// se amanha nascer um tipo novo la, este lado NAO QUEBRA. Um enum tipado
/// aqui estouraria na desserializacao com um valor que ele nao conhece, e o
/// painel inteiro cairia por causa de uma categoria nova.
/// </remarks>
public record OcorrenciaDto(
    [property: JsonPropertyName("id")] Guid Id,
    [property: JsonPropertyName("quandoUtc")] DateTime QuandoUtc,
    [property: JsonPropertyName("sistema")] string Sistema,
    [property: JsonPropertyName("modulo")] string Modulo,
    [property: JsonPropertyName("operacao")] string Operacao,
    [property: JsonPropertyName("tipo")] string Tipo,
    [property: JsonPropertyName("severidade")] string Severidade,
    [property: JsonPropertyName("descricao")] string Descricao,
    [property: JsonPropertyName("dadosEnvolvidosJson")] string? DadosEnvolvidosJson,
    [property: JsonPropertyName("sequenciaJson")] string? SequenciaJson,
    [property: JsonPropertyName("causaProvavel")] string? CausaProvavel,
    [property: JsonPropertyName("causaRaiz")] string? CausaRaiz,
    [property: JsonPropertyName("impacto")] string? Impacto,
    [property: JsonPropertyName("recomendacao")] string? Recomendacao,
    [property: JsonPropertyName("acaoExecutada")] string? AcaoExecutada,
    [property: JsonPropertyName("resultado")] string? Resultado,
    [property: JsonPropertyName("estado")] string Estado,
    [property: JsonPropertyName("correlationId")] Guid CorrelationId,
    [property: JsonPropertyName("vistaEm")] DateTime? VistaEm,
    [property: JsonPropertyName("resolvidaEm")] DateTime? ResolvidaEm,
    [property: JsonPropertyName("resolvidaPor")] string? ResolvidaPor,
    [property: JsonPropertyName("notaDoAdmin")] string? NotaDoAdmin,
    [property: JsonPropertyName("vezesVistas")] int VezesVistas = 1,
    [property: JsonPropertyName("ultimaVezUtc")] DateTime? UltimaVezUtc = null);

/// <summary>O que o sino pergunta.</summary>
public record NaoVistasDto(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("criticas")] int Criticas,
    [property: JsonPropertyName("maisRecente")] DateTime? MaisRecente);
