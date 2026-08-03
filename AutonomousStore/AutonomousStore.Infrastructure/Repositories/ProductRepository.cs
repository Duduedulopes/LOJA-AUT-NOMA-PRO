using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly AutonomousDbContext _context;

    public ProductRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _context.Products.AddAsync(product, cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> UpdatePriceAsync(Guid id, decimal newPrice, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return false;

        product.UpdatePrice(newPrice);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<Product?> UpdateDetailsAsync(Guid id, string name, string? description, string? imageUrl, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return null;

        product.UpdateDetails(name, description, imageUrl);
        await _context.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task<Product?> AssignCategoryAsync(Guid id, Guid? companyId, Guid? categoryId, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return null;

        product.AssignToCompany(companyId);
        product.AssignToCategory(categoryId);
        await _context.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task<Product?> GetByRfidTagAsync(string rfidTag, CancellationToken cancellationToken = default)
    {
        return await _context.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.RfidTag == rfidTag, cancellationToken);
    }

    public async Task<Product?> AssignRfidTagAsync(Guid id, string? rfidTag, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return null;

        product.AssignRfidTag(rfidTag);
        await _context.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task<Product?> DecreaseStockAsync(Guid id, int quantity, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return null;

        product.DecreaseStock(quantity);
        await _context.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task<Product?> IncreaseStockAsync(Guid id, int quantity, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return null;

        product.IncreaseStock(quantity);
        await _context.SaveChangesAsync(cancellationToken);
        return product;
    }

    public async Task<Product?> SetMinimumStockThresholdAsync(Guid id, int? threshold, CancellationToken cancellationToken = default)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null)
            return null;

        product.SetMinimumStockThreshold(threshold);
        await _context.SaveChangesAsync(cancellationToken);
        return product;
    }
}
