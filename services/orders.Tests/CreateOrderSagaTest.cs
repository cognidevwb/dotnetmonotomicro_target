using Orders.Features.CreateOrder;
using Xunit;

namespace Orders.Tests;

/// The CreateOrder saga replaced the monolith's single cross-context SaveChanges.
/// What has to hold is the ORDER of things: the hardest step to reverse (the charge)
/// runs last, and any failure unwinds the completed steps in reverse. That is decided
/// entirely by the saga's message flow, so these tests drive it directly — no host, no
/// database, no container.
public sealed class CreateOrderSagaTest
{
    private static readonly Guid Correlation = new("6f1c7f5a-1d2b-4f3e-9a0c-8e1b2c3d4e5f");

    private static CreateOrderStarted Started(decimal total = 25m) => new(
        CorrelationId: Correlation,
        OrderId: 42,
        CustomerId: 7,
        Lines: [new ReservedLine(ProductId: 10, Quantity: 2, UnitPrice: 12.50m)],
        Total: total);

    [Fact]
    public void Saga_opens_holding_the_reservation_and_charges_last()
    {
        var (saga, next) = CreateOrderSaga.Start(Started());

        // Stock is already reserved when the saga opens, so the saga owns releasing it.
        Assert.True(saga.StockReserved);
        Assert.Null(saga.PaymentId);
        Assert.Equal(42, saga.OrderId);

        // And the only forward step left is the pivot.
        Assert.Equal(new ChargePayment(Correlation, 42, 25m), next);
    }

    [Fact]
    public void Happy_path_completes_the_order_once_the_pivot_succeeds()
    {
        var (saga, _) = CreateOrderSaga.Start(Started());

        var placed = saga.Handle(new PaymentCharged(Correlation, 42, PaymentId: 5001));

        Assert.Equal(new OrderPlaced(Correlation, 42, 25m), placed);
        Assert.Equal(5001, saga.PaymentId);
    }

    /// A failure BEFORE the pivot has landed owes no refund — only the reservation
    /// release and the order cancellation, in that order.
    [Fact]
    public void Failure_before_the_pivot_compensates_stock_then_cancels()
    {
        var (saga, _) = CreateOrderSaga.Start(Started());

        var compensations = saga.Handle(new CreateOrderFailed(Correlation, 42, "charge failed")).ToList();

        Assert.Collection(
            compensations,
            m => Assert.Equal(new ReleaseStock(Correlation, 42, saga.Lines), m),
            m => Assert.Equal(new CancelOrder(Correlation, 42, "charge failed"), m));
    }

    /// A failure AFTER the pivot has landed must unwind in exact reverse of the
    /// forward path: refund the money first, then release stock, then cancel.
    [Fact]
    public void Failure_after_the_pivot_compensates_in_reverse()
    {
        var (saga, _) = CreateOrderSaga.Start(Started());
        saga.Handle(new PaymentCharged(Correlation, 42, PaymentId: 5001));

        var compensations = saga.Handle(new CreateOrderFailed(Correlation, 42, "downstream failed")).ToList();

        Assert.Collection(
            compensations,
            m => Assert.Equal(new RefundPayment(Correlation, 42, 5001), m),
            m => Assert.Equal(new ReleaseStock(Correlation, 42, saga.Lines), m),
            m => Assert.Equal(new CancelOrder(Correlation, 42, "downstream failed"), m));
    }

    [Fact]
    public async Task Charging_the_payment_reports_the_payment_id()
    {
        var payments = new FakePaymentsClient();

        var result = await CreateOrderStepHandlers.Handle(new ChargePayment(Correlation, 42, 25m), payments);

        var charged = Assert.IsType<PaymentCharged>(result);
        Assert.Equal(Correlation, charged.CorrelationId);
        Assert.Equal(42, charged.OrderId);
        Assert.True(charged.PaymentId > 0);
        Assert.Equal((42, 25m), Assert.Single(payments.Charges));
    }

    /// A transport fault at the pivot becomes a saga failure, not an unhandled
    /// exception — that is what triggers the compensations above.
    [Fact]
    public async Task A_failing_charge_turns_into_a_saga_failure()
    {
        var payments = new FakePaymentsClient { Fail = true };

        var result = await CreateOrderStepHandlers.Handle(new ChargePayment(Correlation, 42, 25m), payments);

        var failed = Assert.IsType<CreateOrderFailed>(result);
        Assert.Equal(42, failed.OrderId);
        Assert.Contains("charge failed", failed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Refund_compensation_calls_payments()
    {
        var payments = new FakePaymentsClient();

        await CreateOrderStepHandlers.Handle(new RefundPayment(Correlation, 42, 5001), payments);

        Assert.Equal(5001, Assert.Single(payments.Refunded));
    }

    [Fact]
    public async Task Release_compensation_returns_every_reserved_line()
    {
        var inventory = new FakeInventoryClient();
        var lines = new List<ReservedLine>
        {
            new(ProductId: 10, Quantity: 2, UnitPrice: 12.50m),
            new(ProductId: 11, Quantity: 1, UnitPrice: 5m),
        };

        await CreateOrderStepHandlers.Handle(new ReleaseStock(Correlation, 42, lines), inventory);

        Assert.Equal(new[] { (ProductId: 10, Quantity: 2), (ProductId: 11, Quantity: 1) }, inventory.Released);
    }
}
