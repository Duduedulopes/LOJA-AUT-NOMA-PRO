using System.Net.Http;
using System.Net.Http.Json;
using AutonomousStore.EdgeDesktop.Models;

namespace AutonomousStore.EdgeDesktop.Services;

public class ProductApiService : IProductApiService
{
    private readonly HttpClient _httpClient;

    public ProductApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var products = await _httpClient.GetFromJsonAsync<List<ProductDto>>("api/products", cancellationToken);
        return products ?? [];
    }

    public async Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/products/{id}", cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);
    }

    public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/products", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var product = await response.Content.ReadFromJsonAsync<ProductDto>(cancellationToken);
        return product ?? throw new InvalidOperationException("A API retornou resposta vazia ao criar o produto.");
    }

    public async Task UpdatePriceAsync(Guid id, decimal newPrice, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync(
            $"api/products/{id}/price",
            new UpdateProductPriceRequest(newPrice),
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }
}
