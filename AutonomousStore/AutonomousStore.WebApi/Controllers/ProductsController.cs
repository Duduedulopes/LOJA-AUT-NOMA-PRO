using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductRepository _productRepository;

    public ProductsController(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        Product product;

        try
        {
            product = new Product(
                request.Name,
                request.Barcode,
                request.Price,
                request.CompanyId,
                request.CategoryId,
                request.Description,
                request.ImageUrl,
                request.StockQuantity);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _productRepository.AddAsync(product, cancellationToken);
        await _productRepository.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = product.Id }, ToResponse(product));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        return Ok(products.Select(ToResponse).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProductResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await _productRepository.GetByIdAsync(id, cancellationToken);

        if (product is null)
            return NotFound();

        return Ok(ToResponse(product));
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/price")]
    public async Task<IActionResult> UpdatePrice(
        Guid id,
        UpdateProductPriceRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await _productRepository.UpdatePriceAsync(id, request.Price, cancellationToken);

            if (!updated)
                return NotFound();

            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/details")]
    public async Task<ActionResult<ProductResponse>> UpdateDetails(
        Guid id,
        UpdateProductDetailsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productRepository.UpdateDetailsAsync(id, request.Name, request.Description, request.ImageUrl, cancellationToken);

            if (product is null)
                return NotFound();

            return Ok(ToResponse(product));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/category")]
    public async Task<ActionResult<ProductResponse>> AssignCategory(
        Guid id,
        AssignProductCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.AssignCategoryAsync(id, request.CompanyId, request.CategoryId, cancellationToken);

        if (product is null)
            return NotFound();

        return Ok(ToResponse(product));
    }

    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/rfid-tag")]
    public async Task<ActionResult<ProductResponse>> AssignRfidTag(
        Guid id,
        AssignProductRfidTagRequest request,
        CancellationToken cancellationToken)
    {
        var product = await _productRepository.AssignRfidTagAsync(id, request.RfidTag, cancellationToken);

        if (product is null)
            return NotFound();

        return Ok(ToResponse(product));
    }

    /// <summary>Reposição manual de estoque (ex: chegou mercadoria nova).</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:guid}/stock/restock")]
    public async Task<ActionResult<ProductResponse>> Restock(
        Guid id,
        RestockProductRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productRepository.IncreaseStockAsync(id, request.Quantity, cancellationToken);

            if (product is null)
                return NotFound();

            return Ok(ToResponse(product));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Define a partir de quantas unidades restantes o produto deve ser considerado "estoque baixo".</summary>
    [Authorize(Roles = "Admin")]
    [HttpPatch("{id:guid}/stock/threshold")]
    public async Task<ActionResult<ProductResponse>> SetStockThreshold(
        Guid id,
        SetStockThresholdRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var product = await _productRepository.SetMinimumStockThresholdAsync(id, request.MinimumStockThreshold, cancellationToken);

            if (product is null)
                return NotFound();

            return Ok(ToResponse(product));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Lista os produtos com estoque baixo (abaixo do limite configurado) — atalho útil pro admin.</summary>
    [Authorize(Roles = "Admin")]
    [HttpGet("low-stock")]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetLowStock(CancellationToken cancellationToken)
    {
        var products = await _productRepository.GetAllAsync(cancellationToken);
        var lowStock = products.Where(p => p.IsLowStock).Select(ToResponse).ToList();
        return Ok(lowStock);
    }

    private static ProductResponse ToResponse(Product product) => new(
        product.Id,
        product.Name,
        product.Barcode,
        product.Price,
        product.Description,
        product.ImageUrl,
        product.CompanyId,
        product.CategoryId,
        product.RfidTag,
        product.StockQuantity,
        product.MinimumStockThreshold,
        product.IsLowStock,
        product.IsActive,
        product.CreatedAt);
}
