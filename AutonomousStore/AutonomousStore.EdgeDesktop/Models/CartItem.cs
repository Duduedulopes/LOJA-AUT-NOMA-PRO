namespace AutonomousStore.EdgeDesktop.Models;

public class CartItem
{
    public Guid ProductId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Barcode { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public int Quantity { get; set; }

    public decimal Subtotal => UnitPrice * Quantity;
}
