#nullable enable
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Customers.Features.Customer;

public static class CustomerEndpoints
{
    public static RouteGroupBuilder MapCustomer(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/customers").WithTags("Customers");

        group.MapGet("/{id:int}", async Task<Results<Ok<CustomerResponse>, NotFound>> (
            int id,
            [FromServices] CustomerHandler handler,
            CancellationToken ct) =>
        {
            var customer = await handler.Get(id, ct);
            return customer is null ? TypedResults.NotFound() : TypedResults.Ok(customer);
        }).WithName("GetCustomer");

        // The cross-context activity probe the orders service calls.
        group.MapGet("/{id:int}/active", async Task<Results<Ok<bool>, NotFound>> (
            int id,
            [FromServices] CustomerHandler handler,
            CancellationToken ct) =>
        {
            var active = await handler.IsActive(id, ct);
            return active is null ? TypedResults.NotFound() : TypedResults.Ok(active.Value);
        }).WithName("IsCustomerActive");

        group.MapPost("/", async Task<Results<Created<CustomerResponse>, ValidationProblem, ProblemHttpResult>> (
            CreateCustomerRequest request,
            [FromServices] IValidator<CreateCustomerRequest> validator,
            [FromServices] CustomerHandler handler,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return TypedResults.ValidationProblem(validation.ToDictionary());
            }

            var result = await handler.Create(request, ct);
            return result.Outcome switch
            {
                CustomerWriteOutcome.Succeeded =>
                    TypedResults.Created($"/customers/{result.Customer!.Id}", result.Customer),
                _ => TypedResults.Problem(
                    detail: result.Reason,
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Customer could not be created."),
            };
        }).WithName("CreateCustomer");

        group.MapPut("/{id:int}/active", async Task<Results<Ok<CustomerResponse>, NotFound, ProblemHttpResult>> (
            int id,
            SetCustomerActiveRequest request,
            [FromServices] CustomerHandler handler,
            CancellationToken ct) =>
        {
            var result = await handler.SetActive(id, request, ct);
            return result.Outcome switch
            {
                CustomerWriteOutcome.Succeeded => TypedResults.Ok(result.Customer!),
                CustomerWriteOutcome.NotFound => TypedResults.NotFound(),
                _ => TypedResults.Problem(
                    detail: result.Reason,
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Customer activation could not be changed."),
            };
        }).WithName("SetCustomerActive");

        return group;
    }
}
