using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Categories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICompanyRepository _companyRepository;

    public CategoriesController(ICategoryRepository categoryRepository, ICompanyRepository companyRepository)
    {
        _categoryRepository = categoryRepository;
        _companyRepository = companyRepository;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetByIdAsync(request.CompanyId, cancellationToken);

        if (company is null)
            return BadRequest(new { error = "Empresa não encontrada." });

        Category category;

        try
        {
            category = new Category(request.CompanyId, request.Name, request.Description, request.DisplayOrder);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _categoryRepository.AddAsync(category, cancellationToken);
        await _categoryRepository.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = category.Id }, ToResponse(category));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> GetAll(
        [FromQuery] Guid? companyId,
        CancellationToken cancellationToken)
    {
        var categories = companyId.HasValue
            ? await _categoryRepository.GetByCompanyAsync(companyId.Value, cancellationToken)
            : await _categoryRepository.GetAllAsync(cancellationToken);

        return Ok(categories.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CategoryResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var category = await _categoryRepository.GetByIdAsync(id, cancellationToken);

        if (category is null)
            return NotFound();

        return Ok(ToResponse(category));
    }

    private static CategoryResponse ToResponse(Category category) => new(
        category.Id,
        category.CompanyId,
        category.Name,
        category.Description,
        category.DisplayOrder,
        category.IsActive,
        category.CreatedAt);
}
