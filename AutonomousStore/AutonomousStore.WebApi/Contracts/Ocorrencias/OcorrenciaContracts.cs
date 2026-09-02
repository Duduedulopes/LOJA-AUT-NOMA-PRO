namespace AutonomousStore.WebApi.Contracts.Ocorrencias;

/// <summary>
/// O que trafega. E ESTE o contrato com a tela, nao a entidade.
/// </summary>
/// <remarks>
/// OS ENUMS SAEM COMO TEXTO, e nao como numero. `"tipo": "Roubo"` contra
/// `"tipo": 9`: o segundo obriga quem le o log a ter o codigo em maos, e log
/// serve exatamente para quando o codigo nao esta em maos. Ligado em
/// `Program.cs` com `JsonStringEnumConverter`.
///
/// TODA DATA E UTC. Quem mostra converte para o fuso; quem grava, nunca.
///
/// `Chave` NAO SAI. E controle interno de deduplicacao — a tela nao tem o
/// que fazer com ela, e campo exposto e campo do qual alguem passa a
/// depender.
/// </remarks>
/// <summary>
/// Um erro que estourou no navegador de alguém.
/// </summary>
/// <remarks>
/// ISTO CHEGA DE FORA, SEM LOGIN, E TEM DE SER TRATADO ASSIM.
///
/// A rota é anônima de propósito: o erro que mais acontece é o da tela de
/// login, e exigir token ali seria perder exatamente o que a gente mais
/// precisa ver. O preço disso é que nada aqui é confiável — `App` é
/// conferido contra uma lista fechada, e todo texto é cortado no tamanho da
/// coluna antes de chegar perto do banco.
///
/// `CorrelationId` é o que amarra este relato ao erro do servidor que o
/// causou: o navegador manda o mesmo número que recebeu no 500.
///
/// `ParaOSuporte` é o botão da barra vermelha. Sem ele o erro entra como
/// `Nova` (aparece no histórico); com ele entra como `NoSuporte` (cai na
/// fila do técnico). Quem clica pode não ter login nenhum — é o cliente na
/// loja com a tela quebrada — e por isso a escalada mora aqui, e não na rota
/// de suporte, que exige o papel de admin.
/// </remarks>
public record RelatoDeErroRequest(
    string App,
    string Pagina,
    string Mensagem,
    string? Pilha = null,
    string? Navegador = null,
    Guid? CorrelationId = null,
    bool ParaOSuporte = false,
    string? Contato = null);

/// <summary>A resposta do relato: o número que a pessoa pode citar.</summary>
public record RelatoDeErroResponse(Guid Id, Guid CorrelationId, bool FoiParaOSuporte, int VezesVistas);

public record OcorrenciaResponse(
    Guid Id,
    DateTime QuandoUtc,
    string Sistema,
    string Modulo,
    string Operacao,
    string Tipo,
    string Severidade,
    string Descricao,
    string? DadosEnvolvidosJson,
    string? SequenciaJson,

    /// <summary>INFERENCIA. A tela nao pode dar a isto a mesma cara de `Descricao`.</summary>
    string? CausaProvavel,

    string? CausaRaiz,
    string? Impacto,
    string? Recomendacao,
    string? AcaoExecutada,
    string? Resultado,
    string Estado,
    Guid CorrelationId,
    DateTime? VistaEm,
    DateTime? ResolvidaEm,
    string? ResolvidaPor,
    string? NotaDoAdmin,
    /// <summary>Quantas vezes o mesmo fato aconteceu. 1 quando aconteceu uma vez so.</summary>
    int VezesVistas,

    /// <summary>A ultima repeticao. Nulo quando aconteceu uma vez so — e ai
    /// `QuandoUtc` ja e a resposta inteira.</summary>
    DateTime? UltimaVezUtc);

/// <summary>O que o sino pergunta a cada 20 segundos.</summary>
public record NaoVistasResponse(int Total, int Criticas, DateTime? MaisRecente);

public record ResolverRequest(string? Nota);

public record SuporteRequest(string? DescricaoDoAdmin);

public record ResumoLinha(string Tipo, string Severidade, int Quantidade);

public record ResumoResponse(DateTime Desde, DateTime Ate, int Total, IReadOnlyList<ResumoLinha> Linhas);
