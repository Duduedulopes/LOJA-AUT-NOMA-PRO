namespace AutonomousStore.AdminApp.Models;

// Usados só pra popular os dropdowns de Empresa/Categoria na tela de produtos.
public record CompanyDto(Guid Id, string Name, string? Description, bool IsActive);

public record CategoryDto(Guid Id, Guid CompanyId, string Name, int DisplayOrder, bool IsActive);
