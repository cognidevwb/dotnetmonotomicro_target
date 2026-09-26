#nullable enable
using Catalog.Domain;

namespace Catalog.Acl;

/// Raised when a legacy shape violates a domain invariant at the boundary.
public sealed class LegacyShapeException(string message) : Exception(message);

/// Anti-corruption layer: the monolith's row/DTO shapes for the catalog tables.
/// Legacy naming (CAT_ID, PROD_NAME, stringly-typed statuses) is quarantined here so
/// nothing under Domain/ ever sees it. Pure translation — no EF, no HTTP.
// The record members below deliberately mirror the monolith's column names; that is the
// whole point of this layer, so the naming rule is suppressed here and nowhere else.
#pragma warning disable CA1707 // Identifiers should not contain underscores
public static class CatalogLegacyAcl
{
    public sealed record LegacyCategoryRow(int CAT_ID, string? CAT_NAME);

    public sealed record LegacyProductRow(int PROD_ID, string? PROD_NAME, decimal? PROD_PRICE, int CAT_ID);

    public sealed record LegacyStockRow(int STOCK_ID, int PROD_ID, int? QTY_ON_HAND, string? STOCK_STATUS);

    public static Category ToCategory(LegacyCategoryRow row)
    {
        if (row.CAT_ID <= 0)
        {
            throw new LegacyShapeException($"Category id '{row.CAT_ID}' is not a valid identity.");
        }

        if (string.IsNullOrWhiteSpace(row.CAT_NAME))
        {
            throw new LegacyShapeException($"Category {row.CAT_ID} has no name.");
        }

        return new Category { Id = row.CAT_ID, Name = row.CAT_NAME.Trim() };
    }

    public static Product ToProduct(LegacyProductRow row)
    {
        if (row.PROD_ID <= 0)
        {
            throw new LegacyShapeException($"Product id '{row.PROD_ID}' is not a valid identity.");
        }

        if (string.IsNullOrWhiteSpace(row.PROD_NAME))
        {
            throw new LegacyShapeException($"Product {row.PROD_ID} has no name.");
        }

        var price = row.PROD_PRICE ?? 0m;
        if (price < 0m)
        {
            throw new LegacyShapeException($"Product {row.PROD_ID} has a negative price.");
        }

        if (row.CAT_ID <= 0)
        {
            throw new LegacyShapeException($"Product {row.PROD_ID} is not attached to a category.");
        }

        return new Product
        {
            Id = row.PROD_ID,
            Name = row.PROD_NAME.Trim(),
            Price = price,
            CategoryId = row.CAT_ID
        };
    }

    public static StockItem ToStockItem(LegacyStockRow row)
    {
        if (row.STOCK_ID <= 0)
        {
            throw new LegacyShapeException($"Stock id '{row.STOCK_ID}' is not a valid identity.");
        }

        if (row.PROD_ID <= 0)
        {
            throw new LegacyShapeException($"Stock {row.STOCK_ID} is not attached to a product.");
        }

        var quantity = row.QTY_ON_HAND ?? 0;
        if (quantity < 0)
        {
            throw new LegacyShapeException($"Stock {row.STOCK_ID} has a negative quantity.");
        }

        return new StockItem
        {
            Id = row.STOCK_ID,
            ProductId = row.PROD_ID,
            Quantity = quantity,
            Status = ToStatus(row.STOCK_STATUS, quantity)
        };
    }

    /// The monolith stored status as free text; the domain uses an enum.
    public static StockStatus ToStatus(string? legacyStatus, int quantity) =>
        legacyStatus?.Trim().ToUpperInvariant() switch
        {
            "IN_STOCK" or "IN STOCK" or "A" => StockStatus.InStock,
            "OUT_OF_STOCK" or "OUT OF STOCK" or "O" => StockStatus.OutOfStock,
            "DISCONTINUED" or "D" => StockStatus.Discontinued,
            null or "" => quantity > 0 ? StockStatus.InStock : StockStatus.OutOfStock,
            var other => throw new LegacyShapeException($"Unknown legacy stock status '{other}'.")
        };
}
#pragma warning restore CA1707
