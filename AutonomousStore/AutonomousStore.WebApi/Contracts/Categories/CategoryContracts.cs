namespace AutonomousStore.WebApi.Contracts.Categories;

public record CreateCategoryRequest(Guid CompanyId, string Name, string? Description, int DisplayOrder = 0);

public record UpdateCategoryRequest(string Name, string? Description, int DisplayOrder);

public record CategoryResponse(
    Guid Id,
    Guid CompanyId,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt);
