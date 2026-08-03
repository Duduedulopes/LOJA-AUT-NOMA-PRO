using AutonomousStore.Domain.Entities;

namespace AutonomousStore.Domain.Repositories;

public interface IProductRepository
{
    Task AddAsync(Product product, CancellationToken cancellationToken = default);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<bool> UpdatePriceAsync(Guid id, decimal newPrice, CancellationToken cancellationToken = default);
    Task<Product?> UpdateDetailsAsync(Guid id, string name, string? description, string? imageUrl, CancellationToken cancellationToken = default);
    Task<Product?> AssignCategoryAsync(Guid id, Guid? companyId, Guid? categoryId, CancellationToken cancellationToken = default);
    Task<Product?> GetByRfidTagAsync(string rfidTag, CancellationToken cancellationToken = default);
    Task<Product?> AssignRfidTagAsync(Guid id, string? rfidTag, CancellationToken cancellationToken = default);
    Task<Product?> DecreaseStockAsync(Guid id, int quantity, CancellationToken cancellationToken = default);
    Task<Product?> IncreaseStockAsync(Guid id, int quantity, CancellationToken cancellationToken = default);
    Task<Product?> SetMinimumStockThresholdAsync(Guid id, int? threshold, CancellationToken cancellationToken = default);
}
