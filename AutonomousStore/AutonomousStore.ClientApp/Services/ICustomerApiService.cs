using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

// O cadastro em si (com senha ou Google) fica no IAuthApiService.
// Este serviço cuida do que acontece depois que o cliente já está logado.
public interface ICustomerApiService
{
    Task<CustomerDto?> GetByIdAsync(Guid id);
    Task<(bool Success, CustomerDto? Customer, string? Error)> AddPaymentMethodAsync(Guid customerId, AddPaymentMethodRequest request);
    Task<(bool Success, string? Error)> RemovePaymentMethodAsync(Guid customerId, Guid paymentMethodId);
    Task<(bool Success, string? Error)> SetDefaultPaymentMethodAsync(Guid customerId, Guid paymentMethodId);
    Task<(bool Success, CustomerDto? Customer, string? Error)> UpdateProfileAsync(Guid customerId, UpdateProfileRequest request);
    Task<(bool Success, CustomerDto? Customer, string? Error)> ChangeEmailAsync(Guid customerId, ChangeEmailRequest request);
}
