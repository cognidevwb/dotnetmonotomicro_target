#nullable enable
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Features.Product;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder routes, bool requireAuth)
    {
        var group = routes.MapGroup("/catalog/products").WithTags("Product");
        if (requireAuth)
        {
            group.RequireAuthorization();
        }

        group.MapGet("/", async (int? categoryId, ProductHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.ListAsync(categoryId, ct)));

        group.MapGet("/{id:int}", async Task<Results<Ok<ProductResponse>, NotFound>> (
            int id,
            [FromServices] ProductHandler handler,
            CancellationToken ct) =>
        {
            var found = await handler.GetAsync(id, ct);
            if (found is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(found);
        });

        // The read model orders' typed CatalogClient consumes (PriceOf).
        group.MapGet("/{id:int}/price", async Task<Results<Ok<ProductPriceResponse>, NotFound>> (
            int id,
            [FromServices] ProductHandler handler,
            CancellationToken ct) =>
        {
            var price = await handler.GetPriceAsync(id, ct);
            if (price is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(price);
        });

        group.MapPost("/", async Task<Results<Created<ProductResponse>, ValidationProblem, ProblemHttpResult>> (
            CreateProductRequest request,
            [FromServices] ProductHandler handler,
            [FromServices] IValidator<CreateProductRequest> validator,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return TypedResults.ValidationProblem(validation.ToDictionary());
            }

            var created = await handler.CreateAsync(request, ct);
            if (created is null)
            {
                return TypedResults.Problem(
                    detail: $"Category {request.CategoryId} does not exist.",
                    statusCode: StatusCodes.Status422UnprocessableEntity,
                    title: "Unknown category");
            }

            return TypedResults.Created($"/catalog/products/{created.Id}", created);
        });

        return routes;
    }
}
