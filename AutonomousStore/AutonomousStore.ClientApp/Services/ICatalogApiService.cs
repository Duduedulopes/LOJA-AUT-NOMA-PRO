using AutonomousStore.ClientApp.Models;

namespace AutonomousStore.ClientApp.Services;

public interface ICatalogApiService
{
    Task<List<CompanyDto>> GetCompaniesAsync();
    Task<List<CategoryDto>> GetCategoriesAsync(Guid? companyId = null);
    Task<List<ProductDto>> GetProductsAsync();
}
