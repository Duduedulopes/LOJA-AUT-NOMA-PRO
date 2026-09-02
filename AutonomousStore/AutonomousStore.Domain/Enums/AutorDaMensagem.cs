using System.Text.Json.Serialization;

namespace AutonomousStore.Domain.Enums;

// Texto no fio, e nao numero. O porque esta em SessionStatus.cs.
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AutorDaMensagem
{
    /// <summary>O comprador, falando pelo app do cliente.</summary>
    Cliente = 1,

    /// <summary>O dono da loja, falando pelo painel.</summary>
    Admin = 2,

    /// <summary>O técnico, respondendo pelo ambiente de suporte.</summary>
    Suporte = 3,
}
