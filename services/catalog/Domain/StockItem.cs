#nullable enable
namespace Catalog.Domain;

public enum StockStatus
{
    OutOfStock = 0,
    InStock = 1,
    Discontinued = 2
}

/// Stock carved out of the monolith's Inventory/StockItem.cs — owned by catalog,
/// keyed on its own id, with no navigation to another service's type.
public class StockItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public StockStatus Status { get; set; } = StockStatus.OutOfStock;
}
