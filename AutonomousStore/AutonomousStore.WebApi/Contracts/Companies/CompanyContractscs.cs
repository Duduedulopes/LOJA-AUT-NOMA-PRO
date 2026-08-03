namespace AutonomousStore.WebApi.Contracts.Companies;

public record CreateCompanyRequest(string Name, string? Description, string? LogoUrl, string? ContactEmail, string? ContactPhone);

public record UpdateCompanyRequest(string Name, string? Description, string? LogoUrl, string? ContactEmail, string? ContactPhone);

public record CompanyResponse(
    Guid Id,
    string Name,
    string? Description,
    string? LogoUrl,
    string? ContactEmail,
    string? ContactPhone,
    bool IsActive,
    DateTime CreatedAt);
