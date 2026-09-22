using System.Text.Json.Serialization;

namespace AutonomousStore.Domain.Enums;

// Texto no fio, e nao numero. O porque esta em SessionStatus.cs.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum StatusDoTenant
{
    Ativa = 1,

    // Acesso cortado, dados preservados. E o que o Criador aplica a quem
    // parou de pagar: tudo continua la, so ninguem da empresa entra.
    Suspensa = 2,
}
