using AutonomousStore.Domain.Common;

namespace AutonomousStore.Domain.Entities;

public class SessionItem : Entity
{
    public Guid StoreSessionId { get; private set; }
    public Guid ProductId { get; private set; }

    // Nome e preço são "congelados" no momento da leitura, protegendo o histórico
    // caso o produto mude de nome/preço depois.
    public string ProductName { get; private set; }
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }

    public decimal Subtotal => UnitPrice * Quantity;

    protected SessionItem() { }

    internal SessionItem(Guid storeSessionId, Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentException("A quantidade deve ser maior que zero.", nameof(quantity));

        StoreSessionId = storeSessionId;
        ProductId = productId;
        ProductName = productName;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    internal void IncreaseQuantity(int quantity) => Quantity += quantity;

    internal void DecreaseQuantity(int quantity) => Quantity = Math.Max(0, Quantity - quantity);
}
