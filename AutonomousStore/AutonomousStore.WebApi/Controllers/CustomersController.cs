using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.WebApi.Contracts.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AutonomousStore.WebApi.Controllers;

// O cadastro em si (com senha ou Google) agora fica no AuthController.
// A troca de senha (esqueci minha senha) também fica no AuthController, por e-mail de confirmação.
// Este controller cuida do resto do que acontece DEPOIS que o cliente já está logado.
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerRepository _customerRepository;

    public CustomersController(ICustomerRepository customerRepository)
    {
        _customerRepository = customerRepository;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);

        if (customer is null)
            return NotFound();

        return Ok(ToResponse(customer));
    }

    [HttpPost("{id:guid}/payment-methods")]
    public async Task<ActionResult<CustomerResponse>> AddPaymentMethod(
        Guid id,
        AddPaymentMethodRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);

        if (customer is null)
            return NotFound();

        try
        {
            customer.AddPaymentMethod(request.Type, request.Provider, request.ProviderToken, request.LastFourDigits);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _customerRepository.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(customer));
    }

    [HttpDelete("{id:guid}/payment-methods/{paymentMethodId:guid}")]
    public async Task<ActionResult<CustomerResponse>> RemovePaymentMethod(
        Guid id,
        Guid paymentMethodId,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);

        if (customer is null)
            return NotFound();

        customer.RemovePaymentMethod(paymentMethodId);
        await _customerRepository.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(customer));
    }

    [HttpPost("{id:guid}/payment-methods/{paymentMethodId:guid}/default")]
    public async Task<IActionResult> SetDefaultPaymentMethod(
        Guid id,
        Guid paymentMethodId,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);

        if (customer is null)
            return NotFound();

        try
        {
            customer.SetDefaultPaymentMethod(paymentMethodId);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _customerRepository.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /// <summary>Atualiza nome e telefone do cliente.</summary>
    [HttpPut("{id:guid}/profile")]
    public async Task<ActionResult<CustomerResponse>> UpdateProfile(
        Guid id,
        UpdateProfileRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);

        if (customer is null)
            return NotFound();

        try
        {
            customer.UpdateProfile(request.Name, request.PhoneNumber);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _customerRepository.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(customer));
    }

    /// <summary>Troca o e-mail do cliente (verifica que nenhum outro cadastro já usa o novo e-mail).</summary>
    [HttpPut("{id:guid}/email")]
    public async Task<ActionResult<CustomerResponse>> ChangeEmail(
        Guid id,
        ChangeEmailRequest request,
        CancellationToken cancellationToken)
    {
        var customer = await _customerRepository.GetByIdAsync(id, cancellationToken);

        if (customer is null)
            return NotFound();

        var existing = await _customerRepository.GetByEmailAsync(request.NewEmail, cancellationToken);

        if (existing is not null && existing.Id != id)
            return Conflict(new { error = "Já existe um cliente cadastrado com este e-mail." });

        try
        {
            customer.ChangeEmail(request.NewEmail);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        await _customerRepository.SaveChangesAsync(cancellationToken);

        return Ok(ToResponse(customer));
    }

    private static CustomerResponse ToResponse(Customer customer) => new(
        customer.Id,
        customer.Name,
        customer.Email,
        customer.PhoneNumber,
        customer.IsActive,
        customer.CreatedAt,
        customer.PaymentMethods
            .Select(p => new PaymentMethodResponse(p.Id, p.Type, p.Provider, p.LastFourDigits, p.IsDefault))
            .ToList(),
        customer.PasswordHash is not null);
}
