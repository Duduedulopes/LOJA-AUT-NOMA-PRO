using AutonomousStore.EdgeDesktop.Models;

namespace AutonomousStore.EdgeDesktop.Services;

public interface IProductApiService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken = default);
    Task UpdatePriceAsync(Guid id, decimal newPrice, CancellationToken cancellationToken = default);
}
