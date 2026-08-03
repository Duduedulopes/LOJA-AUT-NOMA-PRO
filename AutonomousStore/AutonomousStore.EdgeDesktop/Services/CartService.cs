using AutonomousStore.EdgeDesktop.Models;

namespace AutonomousStore.EdgeDesktop.Services;

public class CartService : ICartService
{
    private readonly List<CartItem> _items = [];

    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();

    public decimal Total => _items.Sum(i => i.Subtotal);

    public int ItemCount => _items.Sum(i => i.Quantity);

    public void AddProduct(ProductDto product, int quantity = 1)
    {
        if (quantity <= 0)
            throw new ArgumentException("A quantidade deve ser maior que zero.", nameof(quantity));

        var existing = _items.FirstOrDefault(i => i.ProductId == product.Id);

        if (existing is not null)
        {
            existing.Quantity += quantity;
            return;
        }

        _items.Add(new CartItem
        {
            ProductId = product.Id,
            Name = product.Name,
            Barcode = product.Barcode,
            UnitPrice = product.Price,
            Quantity = quantity
        });
    }

    public void RemoveProduct(Guid productId)
    {
        _items.RemoveAll(i => i.ProductId == productId);
    }

    public void UpdateQuantity(Guid productId, int quantity)
    {
        var item = _items.FirstOrDefault(i => i.ProductId == productId);

        if (item is null)
            return;

        if (quantity <= 0)
        {
            RemoveProduct(productId);
            return;
        }

        item.Quantity = quantity;
    }

    public void Clear()
    {
        _items.Clear();
    }
}
