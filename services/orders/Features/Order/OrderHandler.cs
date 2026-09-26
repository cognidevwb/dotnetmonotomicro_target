#nullable enable
using Orders.Features.CreateOrder;
using Orders.Infrastructure;
using OrderEntity = Orders.Domain.Order;

namespace Orders.Features.Order;

/// The Order slice handler: the business rule extracted from the monolith's
/// OrdersController. Stock is no longer this service's to decrement — inventory
/// owns the atomic guarded UPDATE — so a refused reservation comes back as a
/// rejection here and surfaces as 409, never as a read-check-then-write.
public sealed class OrderHandler(OrdersDbContext db, OrderService orders)
{
    public async Task<OrderResponse> PlaceAsync(PlaceOrderRequest request)
    {
        var order = await orders.PlaceOrder(
            request.CustomerId,
            request.Lines.Select(l => (l.ProductId, l.Quantity)));

        return ToResponse(order);
    }

    public async Task<OrderResponse?> GetAsync(int id, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .SingleOrDefaultAsync(o => o.Id == id, ct);

        return order is null ? null : ToResponse(order);
    }

    private static OrderResponse ToResponse(OrderEntity order) => new(
        order.Id,
        order.CustomerId,
        MapStatus(order.Status),
        order.Total,
        [.. order.Lines.Select(l => new OrderLineResponse(l.ProductId, l.Quantity, l.UnitPrice))]);

    private static OrderStatus MapStatus(string status) => status switch
    {
        OrderStatuses.Pending => OrderStatus.Pending,
        OrderStatuses.Placed => OrderStatus.Placed,
        OrderStatuses.Cancelled => OrderStatus.Cancelled,
        _ => OrderStatus.Unknown,
    };
}
