#nullable enable
namespace Orders.Features.Order;

public sealed record PlaceOrderRequest(int CustomerId, IReadOnlyList<OrderLineRequest> Lines);

public sealed record OrderLineRequest(int ProductId, int Quantity);

/// The status the client sees — an enum, not the ported string column.
public enum OrderStatus
{
    Unknown = 0,
    Pending,
    Placed,
    Cancelled,
}

public sealed record OrderResponse(
    int Id,
    int CustomerId,
    OrderStatus Status,
    decimal Total,
    IReadOnlyList<OrderLineResponse> Lines);

public sealed record OrderLineResponse(int ProductId, int Quantity, decimal UnitPrice);

public sealed class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(r => r.CustomerId).GreaterThan(0);
        RuleFor(r => r.Lines).NotEmpty();
        RuleForEach(r => r.Lines).SetValidator(new OrderLineRequestValidator());
    }
}

public sealed class OrderLineRequestValidator : AbstractValidator<OrderLineRequest>
{
    public OrderLineRequestValidator()
    {
        RuleFor(l => l.ProductId).GreaterThan(0);
        RuleFor(l => l.Quantity).GreaterThan(0);
    }
}
