using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Entities;

public class Product : Entity
{
    public string Name { get; private set; }
    public string Barcode { get; private set; }
    public decimal Price { get; private set; }
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsActive { get; private set; }

    // Opcionais por enquanto: o catálogo funciona sem empresa/categoria definidas
    // (produto "solto"), e o app admin é quem preenche isso.
    public Guid? CompanyId { get; private set; }
    public Guid? CategoryId { get; private set; }

    // Tag do chip RFID colado/dentro do produto — vinculada depois de o produto já existir,
    // por isso é sempre opcional aqui (nem todo produto vai ter RFID, alguns usam só código de barras).
    public string? RfidTag { get; private set; }

    // Controle de estoque: quantidade atual em unidades, e um limite opcional pra avisar
    // quando estiver acabando (ex: avisar quando sobrar 5 ou menos).
    public int StockQuantity { get; private set; }
    public int? MinimumStockThreshold { get; private set; }

    public bool IsLowStock => MinimumStockThreshold.HasValue && StockQuantity <= MinimumStockThreshold.Value;

    protected Product() { }

    public Product(
        string name,
        string barcode,
        decimal price,
        Guid? companyId = null,
        Guid? categoryId = null,
        string? description = null,
        string? imageUrl = null,
        int stockQuantity = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do produto não pode ser vazio.", nameof(name));

        if (string.IsNullOrWhiteSpace(barcode))
            throw new ArgumentException("O código de barras não pode ser vazio.", nameof(barcode));

        if (stockQuantity < 0)
            throw new ArgumentException("A quantidade em estoque não pode ser negativa.", nameof(stockQuantity));

        Name = name;
        Barcode = barcode;
        Price = price;
        CompanyId = companyId;
        CategoryId = categoryId;
        Description = description;
        ImageUrl = imageUrl;
        StockQuantity = stockQuantity;
        IsActive = true;
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice < 0)
            throw new ArgumentException("O preço não pode ser negativo.");

        Price = newPrice;
    }

    public void UpdateDetails(string name, string? description, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("O nome do produto não pode ser vazio.", nameof(name));

        Name = name;
        Description = description;
        ImageUrl = imageUrl;
    }

    public void AssignToCompany(Guid? companyId) => CompanyId = companyId;

    public void AssignToCategory(Guid? categoryId) => CategoryId = categoryId;

    public void AssignRfidTag(string? rfidTag) => RfidTag = rfidTag;

    /// <summary>Chamado quando o produto sai fisicamente da prateleira (venda ou retirada).</summary>
    public void DecreaseStock(int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentException("A quantidade precisa ser maior que zero.", nameof(quantity));

        // Nunca deixa negativo — se a leitura de hardware disser que saiu mais do que tínhamos
        // registrado, é sinal de uma divergência de estoque a investigar, não motivo pra travar a venda.
        StockQuantity = Math.Max(0, StockQuantity - quantity);
    }

    /// <summary>Chamado quando o produto volta pra prateleira (devolução) ou é reposto manualmente.</summary>
    public void IncreaseStock(int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentException("A quantidade precisa ser maior que zero.", nameof(quantity));

        StockQuantity += quantity;
    }

    public void SetMinimumStockThreshold(int? threshold)
    {
        if (threshold.HasValue && threshold.Value < 0)
            throw new ArgumentException("O limite mínimo não pode ser negativo.", nameof(threshold));

        MinimumStockThreshold = threshold;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
