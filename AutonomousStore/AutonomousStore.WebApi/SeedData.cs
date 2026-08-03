using AutonomousStore.Domain.Entities;
using AutonomousStore.Domain.Repositories;

namespace AutonomousStore.WebApi;

/// <summary>
/// Popula o banco com uma empresa, categorias e produtos de exemplo — só pra o catálogo do
/// ClientApp não começar vazio durante o desenvolvimento. Idempotente: só roda se ainda não
/// existir nenhuma empresa cadastrada. Substitua/remova antes de ir pra produção de verdade.
/// </summary>
public static class SeedData
{
    public static async Task SeedIfEmptyAsync(IServiceProvider services)
    {
        var companyRepository = services.GetRequiredService<ICompanyRepository>();
        var categoryRepository = services.GetRequiredService<ICategoryRepository>();
        var productRepository = services.GetRequiredService<IProductRepository>();

        var existingCompanies = await companyRepository.GetAllAsync();

        if (existingCompanies.Count > 0)
            return;

        var company = new Company(
            "SmartGo Store — Loja Piloto",
            "Loja autônoma de conveniência, aberta 24h. Produtos de exemplo pra demonstração do sistema.");

        await companyRepository.AddAsync(company);
        await companyRepository.SaveChangesAsync();

        var bebidas = new Category(company.Id, "Bebidas", "Águas, refrigerantes e sucos", displayOrder: 1);
        var snacks = new Category(company.Id, "Snacks", "Salgadinhos, chocolates e barras", displayOrder: 2);
        var padaria = new Category(company.Id, "Padaria", "Itens de padaria embalados", displayOrder: 3);

        await categoryRepository.AddAsync(bebidas);
        await categoryRepository.AddAsync(snacks);
        await categoryRepository.AddAsync(padaria);
        await categoryRepository.SaveChangesAsync();

        var products = new[]
        {
            new Product("Água Mineral 500ml", "7891000000011", 3.50m, company.Id, bebidas.Id,
                "Água mineral sem gás, garrafa 500ml.", stockQuantity: 40),
            new Product("Refrigerante Lata 350ml", "7891000000028", 5.00m, company.Id, bebidas.Id,
                "Refrigerante de cola, lata 350ml gelada.", stockQuantity: 30),
            new Product("Suco Natural 300ml", "7891000000035", 6.50m, company.Id, bebidas.Id,
                "Suco de frutas sem conservantes, garrafa 300ml.", stockQuantity: 20),
            new Product("Café Expresso Lata", "7891000000042", 6.00m, company.Id, bebidas.Id,
                "Café expresso gelado pronto pra beber, lata 250ml.", stockQuantity: 2),
            new Product("Chocolate ao Leite 90g", "7891000000059", 8.90m, company.Id, snacks.Id,
                "Barra de chocolate ao leite, 90g.", stockQuantity: 25),
            new Product("Batata Chips 45g", "7891000000066", 7.50m, company.Id, snacks.Id,
                "Batata chips sabor original, pacote 45g.", stockQuantity: 5),
            new Product("Barra de Cereal", "7891000000073", 4.20m, company.Id, snacks.Id,
                "Barra de cereal com frutas, 25g.", stockQuantity: 35),
            new Product("Sanduíche Natural", "7891000000080", 12.90m, company.Id, padaria.Id,
                "Sanduíche natural de frango, embalado.", stockQuantity: 10),
        };

        foreach (var product in products)
            await productRepository.AddAsync(product);

        await productRepository.SaveChangesAsync();

        // Define limites mínimos nos dois itens que já nasceram com estoque baixo,
        // só pra demonstrar o alerta de "estoque baixo" no painel admin.
        var chips = products.First(p => p.Barcode == "7891000000066");
        var cafe = products.First(p => p.Barcode == "7891000000042");
        chips.SetMinimumStockThreshold(8);
        cafe.SetMinimumStockThreshold(5);

        await productRepository.SaveChangesAsync();
    }
}
