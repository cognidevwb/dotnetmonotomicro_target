#nullable enable
using Catalog.Domain;
using Catalog.Infrastructure;

namespace Catalog.Features.StockItem;

public enum ReserveOutcome
{
    Reserved = 0,
    UnknownProduct = 1,
    InsufficientStock = 2
}

/// The StockItem slice. The monolith read the row, compared, then wrote — a lost-update
/// race under concurrency. Here the decrement is a single guarded UPDATE whose WHERE
/// clause carries the invariant, and zero rows affected means "not enough stock".
public sealed class StockItemHandler(CatalogDbContext db)
{
    public async Task<StockItemResponse?> GetByProductAsync(int productId, CancellationToken ct) =>
        await db.StockItems
            .AsNoTracking()
            .Where(s => s.ProductId == productId)
            .Select(s => new StockItemResponse(s.Id, s.ProductId, s.Quantity, s.Status))
            .FirstOrDefaultAsync(ct);

    public async Task<ReserveOutcome> ReserveAsync(ReserveStockRequest request, CancellationToken ct)
    {
        var quantity = request.Quantity;

        var rows = await db.StockItems
            .Where(s => s.ProductId == request.ProductId
                        && s.Status != StockStatus.Discontinued
                        && s.Quantity >= quantity)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(s => s.Quantity, s => s.Quantity - quantity)
                    .SetProperty(s => s.Status,
                        s => s.Quantity - quantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock),
                ct);

        if (rows > 0)
        {
            return ReserveOutcome.Reserved;
        }

        // Zero rows: either the product has no stock row at all, or the guard rejected it.
        var exists = await db.StockItems.AnyAsync(s => s.ProductId == request.ProductId, ct);
        return exists ? ReserveOutcome.InsufficientStock : ReserveOutcome.UnknownProduct;
    }
}
