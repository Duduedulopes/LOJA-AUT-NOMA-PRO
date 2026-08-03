namespace AutonomousStore.EdgeDesktop.Models;

public record ProductDto(
    Guid Id,
    string Name,
    string Barcode,
    decimal Price,
    bool IsActive,
    DateTime CreatedAt);

public record CreateProductRequest(string Name, string Barcode, decimal Price);

public record UpdateProductPriceRequest(decimal Price);
