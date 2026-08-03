using AutonomousStore.Domain.Enums;

namespace AutonomousStore.ClientApp.Models;

public record AddPaymentMethodRequest(
    PaymentMethodType Type,
    string Provider,
    string ProviderToken,
    string LastFourDigits);

public record PaymentMethodDto(
    Guid Id,
    PaymentMethodType Type,
    string Provider,
    string LastFourDigits,
    bool IsDefault);

public record CustomerDto(
    Guid Id,
    string Name,
    string Email,
    string PhoneNumber,
    bool IsActive,
    DateTime CreatedAt,
    List<PaymentMethodDto> PaymentMethods,
    bool HasPassword);

public record UpdateProfileRequest(string Name, string PhoneNumber);

public record ChangeEmailRequest(string NewEmail);
