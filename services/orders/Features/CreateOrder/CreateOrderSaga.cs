#nullable enable
using Orders.Clients;
using Orders.Infrastructure;

namespace Orders.Features.CreateOrder;

/// Orchestrated CreateOrder saga. The forward path is
/// customer check → reserve stock → charge payment, with the hardest step to
/// reverse (the charge) LAST as the pivot; anything that fails after a step has
/// succeeded compensates in reverse (refund → release stock → cancel order).
/// Saga state and outgoing messages are persisted by the Wolverine EF outbox
/// wired in Program.cs, so state and messages commit atomically.
public class CreateOrderSaga : Saga
{
    public Guid Id { get; set; }

    public int OrderId { get; set; }
    public decimal Total { get; set; }
    public IReadOnlyList<ReservedLine> Lines { get; set; } = [];
    public bool StockReserved { get; set; }
    public int? PaymentId { get; set; }

    /// Stock is reserved by the caller before the saga starts (the reservation is
    /// the inventory service's own atomic guarded decrement), so the saga opens
    /// holding a reservation it is responsible for releasing on failure.
    public static (CreateOrderSaga, ChargePayment) Start(CreateOrderStarted started)
    {
        var saga = new CreateOrderSaga
        {
            Id = started.CorrelationId,
            OrderId = started.OrderId,
            Total = started.Total,
            Lines = started.Lines,
            StockReserved = true,
        };

        return (saga, new ChargePayment(started.CorrelationId, started.OrderId, started.Total));
    }

    /// The pivot succeeded — the order is placed and the saga is done.
    public OrderPlaced Handle(PaymentCharged charged)
    {
        PaymentId = charged.PaymentId;
        MarkCompleted();
        return new OrderPlaced(Id, OrderId, Total);
    }

    /// Anything failed: unwind in reverse — refund a charge if one landed, then
    /// release the reservation, then cancel the order.
    public IEnumerable<object> Handle(CreateOrderFailed failed)
    {
        if (PaymentId is int paymentId)
            yield return new RefundPayment(Id, OrderId, paymentId);

        if (StockReserved)
            yield return new ReleaseStock(Id, OrderId, Lines);

        yield return new CancelOrder(Id, OrderId, failed.Reason);
        MarkCompleted();
    }
}

/// The pivot step, kept out of the saga so a retry re-runs only the remote call.
public sealed record ChargePayment(Guid CorrelationId, int OrderId, decimal Amount);

/// The remote-call side of the saga. Handlers are idempotent-friendly: each is
/// keyed by the order id, and the durable inbox dedups redeliveries.
public static class CreateOrderStepHandlers
{
    public static async Task<object> Handle(ChargePayment command, IPaymentsClient payments)
    {
        try
        {
            var payment = await payments.ChargeAsync(command.OrderId, command.Amount);
            return new PaymentCharged(command.CorrelationId, command.OrderId, payment.Id);
        }
        catch (HttpRequestException ex)
        {
            return new CreateOrderFailed(command.CorrelationId, command.OrderId, $"charge failed: {ex.Message}");
        }
    }

    public static async Task Handle(RefundPayment command, IPaymentsClient payments) =>
        await payments.RefundAsync(command.PaymentId);

    public static async Task Handle(ReleaseStock command, IInventoryClient inventory)
    {
        foreach (var line in command.Lines)
            await inventory.ReleaseAsync(line.ProductId, line.Quantity);
    }

    public static async Task Handle(CancelOrder command, OrdersDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([command.OrderId], ct);
        if (order is null) return;

        order.Status = OrderStatuses.Cancelled;
        await db.SaveChangesAsync(ct);
    }

    public static async Task Handle(OrderPlaced placed, OrdersDbContext db, CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([placed.OrderId], ct);
        if (order is null) return;

        order.Status = OrderStatuses.Placed;
        await db.SaveChangesAsync(ct);
    }
}

/// The persisted status values. Order.Status is a ported string column; these are
/// the only values this service writes, and Features/Order maps them to the
/// OrderStatus enum on the way out.
public static class OrderStatuses
{
    public const string Pending = "pending";
    public const string Placed = "placed";
    public const string Cancelled = "cancelled";
}
