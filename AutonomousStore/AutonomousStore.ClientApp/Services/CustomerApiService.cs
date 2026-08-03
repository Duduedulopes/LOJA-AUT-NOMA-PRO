using System.Net;
using System.Net.Http.Json;
using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

public class CustomerApiService : ICustomerApiService
{
    private readonly HttpClient _http;

    public CustomerApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<CustomerDto?> GetByIdAsync(Guid id)
    {
        var response = await _http.GetAsync($"api/customers/{id}");

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CustomerDto>();
    }

    public async Task<(bool Success, CustomerDto? Customer, string? Error)> AddPaymentMethodAsync(
        Guid customerId,
        AddPaymentMethodRequest request)
    {
        var response = await _http.PostAsJsonAsync($"api/customers/{customerId}/payment-methods", request);

        if (response.IsSuccessStatusCode)
        {
            var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();
            return (true, customer, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, string? Error)> RemovePaymentMethodAsync(Guid customerId, Guid paymentMethodId)
    {
        var response = await _http.DeleteAsync($"api/customers/{customerId}/payment-methods/{paymentMethodId}");
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, string? Error)> SetDefaultPaymentMethodAsync(Guid customerId, Guid paymentMethodId)
    {
        var response = await _http.PostAsync($"api/customers/{customerId}/payment-methods/{paymentMethodId}/default", null);
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, CustomerDto? Customer, string? Error)> UpdateProfileAsync(Guid customerId, UpdateProfileRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/customers/{customerId}/profile", request);

        if (response.IsSuccessStatusCode)
        {
            var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();
            return (true, customer, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, CustomerDto? Customer, string? Error)> ChangeEmailAsync(Guid customerId, ChangeEmailRequest request)
    {
        var response = await _http.PutAsJsonAsync($"api/customers/{customerId}/email", request);

        if (response.IsSuccessStatusCode)
        {
            var customer = await response.Content.ReadFromJsonAsync<CustomerDto>();
            return (true, customer, null);
        }

        return (false, null, await ReadErrorAsync(response));
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return body is not null && body.TryGetValue("error", out var msg)
                ? msg
                : $"Erro inesperado ({(int)response.StatusCode}).";
        }
        catch
        {
            return $"Erro inesperado ({(int)response.StatusCode}).";
        }
    }
}
