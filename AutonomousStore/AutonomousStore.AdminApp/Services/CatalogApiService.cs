using System.Net.Http.Json;
using AutonomousStore.AdminApp.Models;

namespace AutonomousStore.AdminApp.Services;

// Só pra popular os dropdowns de Empresa/Categoria na tela de produtos do admin.
public interface ICatalogApiService
{
    Task<List<CompanyDto>> GetCompaniesAsync();
    Task<List<CategoryDto>> GetCategoriesAsync(Guid? companyId = null);
}

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
}
