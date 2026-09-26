#nullable enable
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Features.Category;

public static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder routes, bool requireAuth)
    {
        var group = routes.MapGroup("/catalog/categories").WithTags("Category");
        if (requireAuth)
        {
            group.RequireAuthorization();
        }

        group.MapGet("/", async (CategoryHandler handler, CancellationToken ct) =>
            TypedResults.Ok(await handler.ListAsync(ct)));

        group.MapGet("/{id:int}", async Task<Results<Ok<CategoryResponse>, NotFound>> (
            int id,
            [FromServices] CategoryHandler handler,
            CancellationToken ct) =>
        {
            var found = await handler.GetAsync(id, ct);
            if (found is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(found);
        });

        group.MapPost("/", async Task<Results<Created<CategoryResponse>, ValidationProblem>> (
            CreateCategoryRequest request,
            [FromServices] CategoryHandler handler,
            [FromServices] IValidator<CreateCategoryRequest> validator,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return TypedResults.ValidationProblem(validation.ToDictionary());
            }

            var created = await handler.CreateAsync(request, ct);
            return TypedResults.Created($"/catalog/categories/{created.Id}", created);
        });

        return routes;
    }
}
