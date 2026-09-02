using System.Text.Json.Serialization;

namespace AutonomousStore.Domain.Enums;

/// <summary>Onde a ocorrencia esta na vida dela.</summary>
// Texto no fio, e nao numero. O porque esta em SessionStatus.cs.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EstadoDaOcorrencia
{
    Nova = 1,
    Vista = 2,
    EmAnalise = 3,
    Resolvida = 4,

    /// <summary>O Chefe olhou e decidiu que nao e problema.</summary>
    Ignorada = 5,

    /// <summary>Foi para o suporte tecnico.</summary>
    NoSuporte = 6,
}
