using AutonomousStore.EdgeDesktop.Models;

namespace AutonomousStore.EdgeDesktop.Services;

public interface ICartService
{
    IReadOnlyList<CartItem> Items { get; }
    decimal Total { get; }
    int ItemCount { get; }
    void AddProduct(ProductDto product, int quantity = 1);
    void RemoveProduct(Guid productId);
    void UpdateQuantity(Guid productId, int quantity);
    void Clear();
}
