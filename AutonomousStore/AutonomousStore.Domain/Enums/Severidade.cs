using System.Text.Json.Serialization;

namespace AutonomousStore.Domain.Enums;

/// <summary>Quanto isto atrapalha, e com que pressa o Chefe precisa saber.</summary>
// Texto no fio, e nao numero. O porque esta em SessionStatus.cs.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Severidade
{
    /// <summary>Fica no historico. Ninguem precisa olhar hoje.</summary>
    Informativa = 1,

    /// <summary>Vale conferir quando sobrar tempo.</summary>
    Baixa = 2,

    /// <summary>Ja custa dinheiro ou confianca no numero.</summary>
    Media = 3,

    /// <summary>Precisa de alguem hoje.</summary>
    Alta = 4,

    /// <summary>Acende o sino em vermelho. Roubo entra aqui.</summary>
    Critica = 5,
}
