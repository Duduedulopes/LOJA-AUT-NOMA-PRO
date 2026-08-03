using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Companies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyRepository _companyRepository;

    public CompaniesController(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<CompanyResponse>> Create(CreateCompanyRequest request, CancellationToken cancellationToken)
    {
        Company company;

        try
        {
            company = new Company(request.Name, request.Description, request.LogoUrl, request.ContactEmail, request.ContactPhone);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _companyRepository.AddAsync(company, cancellationToken);
        await _companyRepository.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = company.Id }, ToResponse(company));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var companies = await _companyRepository.GetAllAsync(cancellationToken);
        return Ok(companies.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CompanyResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(id, cancellationToken);

        if (company is null)
            return NotFound();

        return Ok(ToResponse(company));
    }

    private static CompanyResponse ToResponse(Company company) => new(
        company.Id,
        company.Name,
        company.Description,
        company.LogoUrl,
        company.ContactEmail,
        company.ContactPhone,
        company.IsActive,
        company.CreatedAt);
}
