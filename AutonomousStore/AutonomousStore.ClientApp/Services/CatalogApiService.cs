using System.Net.Http.Json;
using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

public class CatalogApiService : ICatalogApiService
{
    private readonly HttpClient _http;

    public CatalogApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CompanyDto>> GetCompaniesAsync()
    {
        var result = await _http.GetFromJsonAsync<List<CompanyDto>>("api/companies");
        return result ?? new();
    }

    public async Task<List<CategoryDto>> GetCategoriesAsync(Guid? companyId = null)
    {
        var url = companyId.HasValue ? $"api/categories?companyId={companyId}" : "api/categories";
        var result = await _http.GetFromJsonAsync<List<CategoryDto>>(url);
        return result ?? new();
    }

    public async Task<List<ProductDto>> GetProductsAsync()
    {
        var result = await _http.GetFromJsonAsync<List<ProductDto>>("api/products");
        return result ?? new();
    }
}
