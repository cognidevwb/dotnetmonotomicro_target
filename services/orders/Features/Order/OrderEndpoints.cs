#nullable enable
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Orders.Features.Order;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder routes, bool requireAuth)
    {
        var group = routes.MapGroup("/orders").WithTags("Orders");
        if (requireAuth)
            group.RequireAuthorization();

        group.MapPost("/", async Task<Results<Created<OrderResponse>, ValidationProblem, ProblemHttpResult>> (
            PlaceOrderRequest request,
            [FromServices] IValidator<PlaceOrderRequest> validator,
            [FromServices] OrderHandler handler) =>
        {
            var validation = await validator.ValidateAsync(request);
            if (!validation.IsValid)
                return TypedResults.ValidationProblem(validation.ToDictionary());

            try
            {
                var order = await handler.PlaceAsync(request);
                return TypedResults.Created($"/orders/{order.Id}", order);
            }
            catch (OrderRejectedException ex)
            {
                // A participant refused for a business reason (inactive customer,
                // insufficient stock) — the client's request conflicts with state.
                return TypedResults.Problem(
                    title: "Order rejected",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status409Conflict);
            }
        })
        .WithName("PlaceOrder");

        group.MapGet("/{id:int}", async Task<Results<Ok<OrderResponse>, NotFound>> (
            int id,
            [FromServices] OrderHandler handler,
            CancellationToken ct) =>
        {
            var order = await handler.GetAsync(id, ct);
            return order is not null ? TypedResults.Ok(order) : TypedResults.NotFound();
        })
        .WithName("GetOrder");

        return routes;
    }
}
