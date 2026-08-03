namespace AutonomousStore.ClientApp.Models;

public record CompanyDto(
    Guid Id,
    string Name,
    string? Description,
    string? LogoUrl,
    string? ContactEmail,
    string? ContactPhone,
    bool IsActive,
    DateTime CreatedAt);

public record CategoryDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt);

public record ProductDto(
    Guid Id,
    string Name,
    string Barcode,
    decimal Price,
    string? Description,
    string? ImageUrl,
    Guid? CompanyId,
    Guid? CategoryId,
    bool IsActive,
    DateTime CreatedAt);
