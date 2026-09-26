#nullable enable
using Payments.Acl;

namespace Payments.Features.Payment;

/// Charge request as it arrives from the gateway — never trusted, always validated.
public sealed record ChargePaymentRequest(int OrderId, decimal Amount);

public sealed record PaymentResponse(int Id, int OrderId, decimal Amount, PaymentStatus Status);

public sealed class ChargePaymentRequestValidator : AbstractValidator<ChargePaymentRequest>
{
    public ChargePaymentRequestValidator()
    {
        RuleFor(x => x.OrderId)
            .GreaterThan(0)
            .WithMessage("OrderId must be a positive order reference.");

        RuleFor(x => x.Amount)
            .GreaterThan(0m)
            .WithMessage("Amount must be greater than zero.");
    }
}
