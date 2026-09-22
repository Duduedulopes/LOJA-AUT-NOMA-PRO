using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;
using AutonomousStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AutonomousStore.Infrastructure.Repositories;

public class CustomerRepository : ICustomerRepository
{
    private readonly AutonomousDbContext _context;

    public CustomerRepository(AutonomousDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await _context.Customers.AddAsync(customer, cancellationToken);
    }

    public async Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .Include(c => c.PaymentMethods)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .Include(c => c.PaymentMethods)
            .FirstOrDefaultAsync(c => c.Email == email, cancellationToken);
    }

    public async Task<Customer?> GetByCpfAsync(string cpf, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .FirstOrDefaultAsync(c => c.Cpf == cpf, cancellationToken);
    }

    public async Task<Customer?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default)
    {
        return await _context.Customers
            .Include(c => c.PaymentMethods)
            .FirstOrDefaultAsync(c => c.GoogleId == googleId, cancellationToken);
    }

    public async Task<IReadOnlyList<Customer>> ListarAsync(
        Guid? empresaId, string? busca, int limite, CancellationToken cancellationToken = default)
    {
        var q = _context.Customers.AsNoTracking().AsQueryable();

        if (empresaId is { } empresa)
            q = q.Where(c => c.TenantId == empresa);

        if (!string.IsNullOrWhiteSpace(busca))
        {
            // Sem diferenciar maiuscula de minuscula, e dito AQUI: depender da collation do servidor
            // faria "souza" achar "Souza" no SQL Server e nao achar em outro banco.
            var termo = busca.Trim().ToLower();
            q = q.Where(c => c.Name.ToLower().Contains(termo) || c.Email.ToLower().Contains(termo));
        }

        return await q
            .OrderBy(c => c.Name)
            .Take(Math.Clamp(limite, 1, 200))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> ContarPorEmpresaAsync(CancellationToken cancellationToken = default)
    {
        var linhas = await _context.Customers
            .AsNoTracking()
            .Where(c => c.TenantId != null)
            .GroupBy(c => c.TenantId)
            .Select(g => new { Empresa = g.Key, Quantidade = g.Count() })
            .ToListAsync(cancellationToken);

        return linhas.ToDictionary(l => l.Empresa!.Value, l => l.Quantidade);
    }

    public async Task<bool> ExisteNaEmpresaAsync(
        Guid empresaId, string email, string cpf, CancellationToken cancellationToken = default)
    {
        var emailAlvo = (email ?? "").Trim().ToLower();

        return await _context.Customers
            .AsNoTracking()
            .AnyAsync(
                c => c.TenantId == empresaId && (c.Email.ToLower() == emailAlvo || c.Cpf == cpf),
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
