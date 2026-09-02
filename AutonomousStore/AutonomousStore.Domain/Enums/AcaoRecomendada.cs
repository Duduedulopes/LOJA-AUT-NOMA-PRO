using System.Text.Json.Serialization;

namespace AutonomousStore.Domain.Enums;

/// <summary>O que o sistema propoe fazer a respeito.</summary>
// Texto no fio, e nao numero. O porque esta em SessionStatus.cs.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AcaoRecomendada
{
    ApenasRegistrar = 1,
    SugerirCorrecao = 2,
    SolicitarAprovacao = 3,
    BloquearOperacao = 4,

    /// <summary>
    /// RESERVADO. Existe no enum e NENHUM detector usa na versao 1.
    /// </summary>
    /// <remarks>
    /// O motivo e medido, nao teorico. Neste projeto, `decimal.TryParse` sem
    /// `CultureInfo.InvariantCulture` leu "3.00" como 300 em pt-BR: o gerente
    /// mostrou "R$ 3,00" na confirmacao e gravou R$ 300,00 no banco, duas
    /// vezes seguidas, com confianca total. O teste passava porque o
    /// ambiente de teste roda em cultura invariante.
    ///
    /// Um detector ganha o direito de corrigir sozinho quando tiver TAXA DE
    /// FALSO POSITIVO MEDIDA — do mesmo jeito que o gerente so grava acima
    /// de um limiar que o calibrador mediu e escreveu dentro do modelo.
    /// </remarks>
    CorrigirAutomaticamente = 5,
}
