using AutonomousStore.Domain.Common;
using AutonomousStore.Domain.Enums;

namespace AutonomousStore.Domain.Entities;

public class PaymentMethod : Entity
{
    public Guid CustomerId { get; private set; }
    public PaymentMethodType Type { get; private set; }

    // Nome do provedor de pagamento (ex: "Stripe", "MercadoPago")
    public string Provider { get; private set; }

    // Token/id retornado pelo gateway de pagamento — nunca armazenar dados de cartão diretamente aqui
    public string ProviderToken { get; private set; }
    public string LastFourDigits { get; private set; }
    public bool IsDefault { get; private set; }

    protected PaymentMethod() { }

    internal PaymentMethod(
        Guid customerId,
        PaymentMethodType type,
        string provider,
        string providerToken,
        string lastFourDigits,
        bool isDefault)
    {
        if (string.IsNullOrWhiteSpace(providerToken))
            throw new ArgumentException("O token do provedor de pagamento é obrigatório.", nameof(providerToken));

        CustomerId = customerId;
        Type = type;
        Provider = provider;
        ProviderToken = providerToken;
        LastFourDigits = lastFourDigits;
        IsDefault = isDefault;
    }

    internal void SetAsDefault() => IsDefault = true;

    internal void UnsetAsDefault() => IsDefault = false;
}
