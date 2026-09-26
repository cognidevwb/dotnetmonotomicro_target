#nullable enable
using Orders.Features.CreateOrder;
using OrderEntity = Orders.Domain.Order;
using OrderLineEntity = Orders.Domain.OrderLine;

namespace Orders.Acl;

/// Raised when a legacy row cannot be translated without violating a domain
/// invariant. Rejecting at the boundary keeps the bad shape out of Domain/.
public sealed class LegacyTranslationException(string message) : Exception(message);

/// The legacy monolith's persistence shapes for Order/OrderLine, quarantined here.
/// Nothing outside this file may name these columns.
public sealed record LegacyOrderRow(
    int ORDER_ID,
    int CUST_ID,
    string? ORDER_STATUS,
    decimal ORDER_TOTAL,
    IReadOnlyList<LegacyOrderLineRow>? LINES);

public sealed record LegacyOrderLineRow(
    int LINE_ID,
    int ORDER_ID,
    int PROD_ID,
    int QTY,
    decimal UNIT_PRICE);

/// Anti-corruption layer: pure translation, no EF and no HTTP.
public static class OrdersLegacyAcl
{
    public static OrderEntity ToDomain(LegacyOrderRow row)
    {
        if (row.ORDER_ID <= 0)
            throw new LegacyTranslationException("legacy order has no identity");
        if (row.CUST_ID <= 0)
            throw new LegacyTranslationException($"legacy order {row.ORDER_ID} has no customer");
        if (row.ORDER_TOTAL < 0)
            throw new LegacyTranslationException($"legacy order {row.ORDER_ID} has a negative total");

        var lines = (row.LINES ?? []).Select(ToDomain).ToList();

        return new OrderEntity
        {
            Id = row.ORDER_ID,
            CustomerId = row.CUST_ID,
            Status = ToStatus(row.ORDER_STATUS, row.ORDER_ID),
            Total = row.ORDER_TOTAL,
            Lines = lines,
        };
    }

    public static OrderLineEntity ToDomain(LegacyOrderLineRow row)
    {
        if (row.PROD_ID <= 0)
            throw new LegacyTranslationException($"legacy line {row.LINE_ID} has no product");
        if (row.QTY <= 0)
            throw new LegacyTranslationException($"legacy line {row.LINE_ID} has a non-positive quantity");
        if (row.UNIT_PRICE < 0)
            throw new LegacyTranslationException($"legacy line {row.LINE_ID} has a negative unit price");

        return new OrderLineEntity
        {
            Id = row.LINE_ID,
            OrderId = row.ORDER_ID,
            ProductId = row.PROD_ID,
            Quantity = row.QTY,
            UnitPrice = row.UNIT_PRICE,
        };
    }

    /// The legacy column is stringly-typed and inconsistently cased; the domain
    /// accepts only the three values this service writes.
    private static string ToStatus(string? legacyStatus, int orderId) =>
        (legacyStatus ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "pending" or "new" or "open" => OrderStatuses.Pending,
            "placed" or "complete" or "completed" => OrderStatuses.Placed,
            "cancelled" or "canceled" or "void" => OrderStatuses.Cancelled,
            var other => throw new LegacyTranslationException(
                $"legacy order {orderId} has unmappable status '{other}'"),
        };
}
