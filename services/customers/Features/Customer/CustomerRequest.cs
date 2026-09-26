#nullable enable
namespace Customers.Features.Customer;

/// Register a customer. Never trust the client — the email is validated here.
public sealed record CreateCustomerRequest(string Email, bool Active = true);

/// Change a customer's activation state.
public sealed record SetCustomerActiveRequest(bool Active);

/// The customer read model returned to callers (including the orders service's
/// IsActive probe). Kept separate from the entity so persistence shape can move.
public sealed record CustomerResponse(int Id, string Email, bool Active);

public sealed class CreateCustomerRequestValidator : AbstractValidator<CreateCustomerRequest>
{
    public CreateCustomerRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .MaximumLength(256).WithMessage("Email must be 256 characters or fewer.")
            .EmailAddress().WithMessage("Email must be a valid address.");
    }
}
