#nullable enable
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Features.StockItem;

public static class StockItemEndpoints
{
    public static IEndpointRouteBuilder MapStockItemEndpoints(this IEndpointRouteBuilder routes, bool requireAuth)
    {
        var group = routes.MapGroup("/catalog/stock").WithTags("StockItem");
        if (requireAuth)
        {
            group.RequireAuthorization();
        }

        group.MapGet("/{productId:int}", async Task<Results<Ok<StockItemResponse>, NotFound>> (
            int productId,
            [FromServices] StockItemHandler handler,
            CancellationToken ct) =>
        {
            var found = await handler.GetByProductAsync(productId, ct);
            if (found is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(found);
        });

        group.MapPost("/reservations", async Task<Results<Ok<StockItemResponse>, ValidationProblem, NotFound, ProblemHttpResult>> (
            ReserveStockRequest request,
            [FromServices] StockItemHandler handler,
            [FromServices] IValidator<ReserveStockRequest> validator,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return TypedResults.ValidationProblem(validation.ToDictionary());
            }

            var outcome = await handler.ReserveAsync(request, ct);
            if (outcome == ReserveOutcome.UnknownProduct)
            {
                return TypedResults.NotFound();
            }

            if (outcome == ReserveOutcome.InsufficientStock)
            {
                return TypedResults.Problem(
                    detail: $"Not enough stock for product {request.ProductId} to reserve {request.Quantity}.",
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Insufficient stock");
            }

            var current = await handler.GetByProductAsync(request.ProductId, ct);
            if (current is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(current);
        });

        return routes;
    }
}
