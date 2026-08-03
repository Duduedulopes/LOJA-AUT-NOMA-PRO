using AutonomousStore.Domain.Enums;

namespace AutonomousStore.WebApi.Contracts.Customers;

public record AddPaymentMethodRequest(
    PaymentMethodType Type,
    string Provider,
    string ProviderToken,
    string LastFourDigits);

public record PaymentMethodResponse(
    Guid Id,
    PaymentMethodType Type,
    string Provider,
    string LastFourDigits,
    bool IsDefault);

public record CustomerResponse(
    Guid Id,
    string Name,
    string Email,
    string PhoneNumber,
    bool IsActive,
    DateTime CreatedAt,
    IReadOnlyList<PaymentMethodResponse> PaymentMethods,
    bool HasPassword);

public record UpdateProfileRequest(string Name, string PhoneNumber);

public record ChangeEmailRequest(string NewEmail);
