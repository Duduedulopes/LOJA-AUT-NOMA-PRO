using System.Text.Json.Serialization;

namespace AutonomousStore.Domain.Enums;

// Texto no fio, e nao numero. O porque esta em SessionStatus.cs.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PaymentMethodType
{
    CartaoCredito = 1,
    CartaoDebito = 2,
    Pix = 3
}
