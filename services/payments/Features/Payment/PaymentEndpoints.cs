#nullable enable
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Payments.Features.Payment;

public static class PaymentEndpoints
{
    public static RouteGroupBuilder MapPayment(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/payments").WithTags("Payments");

        group.MapPost("/charge", async Task<Results<Created<PaymentResponse>, ValidationProblem, Conflict<ProblemDetails>>> (
            ChargePaymentRequest request,
            [FromServices] IValidator<ChargePaymentRequest> validator,
            [FromServices] PaymentHandler handler,
            CancellationToken ct) =>
        {
            var validation = await validator.ValidateAsync(request, ct);
            if (!validation.IsValid)
            {
                return TypedResults.ValidationProblem(validation.ToDictionary());
            }

            try
            {
                var payment = await handler.ChargeAsync(request, ct);
                return TypedResults.Created($"/payments/{payment.Id}", payment);
            }
            catch (PaymentAlreadyExistsException ex)
            {
                return TypedResults.Conflict(new ProblemDetails
                {
                    Status = StatusCodes.Status409Conflict,
                    Title = "Order already charged",
                    Detail = ex.Message
                });
            }
        })
        .WithName("ChargePayment");

        group.MapGet("/{id:int}", async Task<Results<Ok<PaymentResponse>, NotFound>> (
            int id,
            [FromServices] PaymentHandler handler,
            CancellationToken ct) =>
        {
            var payment = await handler.GetAsync(id, ct);
            if (payment is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(payment);
        })
        .WithName("GetPayment");

        group.MapGet("/by-order/{orderId:int}", async Task<Results<Ok<PaymentResponse>, NotFound>> (
            int orderId,
            [FromServices] PaymentHandler handler,
            CancellationToken ct) =>
        {
            var payment = await handler.GetByOrderAsync(orderId, ct);
            if (payment is null)
            {
                return TypedResults.NotFound();
            }

            return TypedResults.Ok(payment);
        })
        .WithName("GetPaymentByOrder");

        return group;
    }
}
