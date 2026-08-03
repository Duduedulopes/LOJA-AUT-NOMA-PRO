using System.Net.Http.Json;
using AutonomousStore.AdminApp.Models;

namespace AutonomousStore.AdminApp.Services;

public interface IProductApiService
{
    Task<List<ProductDto>> GetAllAsync();
    Task<List<ProductDto>> GetLowStockAsync();
    Task<(bool Success, ProductDto? Product, string? Error)> CreateAsync(CreateProductRequest request);
    Task<(bool Success, string? Error)> UpdatePriceAsync(Guid id, decimal price);
    Task<(bool Success, string? Error)> UpdateDetailsAsync(Guid id, string name, string? description, string? imageUrl);
    Task<(bool Success, string? Error)> RestockAsync(Guid id, int quantity);
    Task<(bool Success, string? Error)> SetStockThresholdAsync(Guid id, int? threshold);
    Task<(bool Success, string? Error)> AssignRfidTagAsync(Guid id, string? rfidTag);
}

public class ProductApiService : IProductApiService
{
    private readonly HttpClient _http;

    public ProductApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<ProductDto>> GetAllAsync()
    {
        var result = await _http.GetFromJsonAsync<List<ProductDto>>("api/products");
        return result ?? new();
    }

    public async Task<List<ProductDto>> GetLowStockAsync()
    {
        var result = await _http.GetFromJsonAsync<List<ProductDto>>("api/products/low-stock");
        return result ?? new();
    }

    public async Task<(bool Success, ProductDto? Product, string? Error)> CreateAsync(CreateProductRequest request)
    {
        var response = await _http.PostAsJsonAsync("api/products", request);

        if (!response.IsSuccessStatusCode)
            return (false, null, await ReadErrorAsync(response));

        var product = await response.Content.ReadFromJsonAsync<ProductDto>();
        return (true, product, null);
    }

    public async Task<(bool Success, string? Error)> UpdatePriceAsync(Guid id, decimal price)
    {
        var response = await _http.PatchAsJsonAsync($"api/products/{id}/price", new UpdateProductPriceRequest(price));
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, string? Error)> UpdateDetailsAsync(Guid id, string name, string? description, string? imageUrl)
    {
        var response = await _http.PatchAsJsonAsync($"api/products/{id}/details", new UpdateProductDetailsRequest(name, description, imageUrl));
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, string? Error)> RestockAsync(Guid id, int quantity)
    {
        var response = await _http.PostAsJsonAsync($"api/products/{id}/stock/restock", new RestockProductRequest(quantity));
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    public async Task<(bool Success, string? Error)> SetStockThresholdAsync(Guid id, int? threshold)
    {
        var response = await _http.PatchAsJsonAsync($"api/products/{id}/stock/threshold", new SetStockThresholdRequest(threshold));
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    /// <summary>Vincula o UID lido do chip RFID ao produto. Passar vazio desvincula a tag.</summary>
    public async Task<(bool Success, string? Error)> AssignRfidTagAsync(Guid id, string? rfidTag)
    {
        var normalized = string.IsNullOrWhiteSpace(rfidTag) ? null : rfidTag.Trim().ToUpperInvariant();

        var response = await _http.PatchAsJsonAsync($"api/products/{id}/rfid-tag", new AssignProductRfidTagRequest(normalized));
        return response.IsSuccessStatusCode ? (true, null) : (false, await ReadErrorAsync(response));
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            return body is not null && body.TryGetValue("error", out var msg) ? msg : $"Erro ({(int)response.StatusCode}).";
        }
        catch
        {
            return $"Erro ({(int)response.StatusCode}).";
        }
    }
}
