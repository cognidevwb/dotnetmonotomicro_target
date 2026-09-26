#nullable enable
using Catalog.Domain;

namespace Catalog.Features.StockItem;

public sealed record ReserveStockRequest(int ProductId, int Quantity);

public sealed record StockItemResponse(int Id, int ProductId, int Quantity, StockStatus Status);

public sealed class ReserveStockRequestValidator : AbstractValidator<ReserveStockRequest>
{
    public ReserveStockRequestValidator()
    {
        RuleFor(r => r.ProductId).GreaterThan(0);
        RuleFor(r => r.Quantity).GreaterThan(0);
    }
}
