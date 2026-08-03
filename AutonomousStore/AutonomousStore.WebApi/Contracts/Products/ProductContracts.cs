namespace AutonomousStore.WebApi.Contracts.Products;

public record CreateProductRequest(
    string Name,
    string Barcode,
    decimal Price,
    Guid? CompanyId = null,
    Guid? CategoryId = null,
    string? Description = null,
    string? ImageUrl = null,
    int StockQuantity = 0);

public record UpdateProductPriceRequest(decimal Price);

public record UpdateProductDetailsRequest(string Name, string? Description, string? ImageUrl);

public record AssignProductCategoryRequest(Guid? CompanyId, Guid? CategoryId);

public record AssignProductRfidTagRequest(string? RfidTag);

public record RestockProductRequest(int Quantity);

public record SetStockThresholdRequest(int? MinimumStockThreshold);

public record ProductResponse(
    Guid Id,
    string Name,
    string Barcode,
    decimal Price,
    string? Description,
    string? ImageUrl,
    Guid? CompanyId,
    Guid? CategoryId,
    string? RfidTag,
    int StockQuantity,
    int? MinimumStockThreshold,
    bool IsLowStock,
    bool IsActive,
    DateTime CreatedAt);
