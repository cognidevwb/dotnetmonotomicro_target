#nullable enable
namespace Orders.Features.CreateOrder;

/// One reserved line of a CreateOrder saga run. Carried on every message so a
/// redelivery can compensate without re-reading the order.
public sealed record ReservedLine(int ProductId, int Quantity, decimal UnitPrice);

/// Saga start: the order rows are already persisted as `pending` in this service's
/// own DbContext; the remote steps still have to run.
public sealed record CreateOrderStarted(
    Guid CorrelationId,
    int OrderId,
    int CustomerId,
    IReadOnlyList<ReservedLine> Lines,
    decimal Total);

// --- forward steps (the pivot — charging — is last) -------------------------
public sealed record PaymentCharged(Guid CorrelationId, int OrderId, int PaymentId);
public sealed record OrderPlaced(Guid CorrelationId, int OrderId, decimal Total);

// --- failures ---------------------------------------------------------------
public sealed record CreateOrderFailed(Guid CorrelationId, int OrderId, string Reason);

// --- compensations, applied in reverse order of the forward steps -----------
public sealed record RefundPayment(Guid CorrelationId, int OrderId, int PaymentId);
public sealed record ReleaseStock(Guid CorrelationId, int OrderId, IReadOnlyList<ReservedLine> Lines);
public sealed record CancelOrder(Guid CorrelationId, int OrderId, string Reason);
