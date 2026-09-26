#nullable enable
using Customers.Infrastructure;
using CustomerEntity = Customers.Domain.Customer;

namespace Customers.Features.Customer;

/// Outcome of a write that can legitimately fail on state rather than on shape.
public enum CustomerWriteOutcome
{
    Succeeded,
    NotFound,
    Conflict,
}

public sealed record CustomerWriteResult(CustomerWriteOutcome Outcome, CustomerResponse? Customer, string? Reason);

/// The Customer slice handler — the business rules extracted from the monolith's
/// CustomerService/CustomersController. Owns customersdb only; no cross-service reads.
public sealed class CustomerHandler(CustomersDbContext db)
{
    /// Ported from CustomerService.Get.
    public async Task<CustomerResponse?> Get(int id, CancellationToken ct)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        return customer is null ? null : ToResponse(customer);
    }

    /// Ported from CustomerService.IsActive — the probe the orders service calls.
    public async Task<bool?> IsActive(int id, CancellationToken ct)
    {
        var rows = await db.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => (bool?)c.Active)
            .FirstOrDefaultAsync(ct);

        return rows;
    }

    public async Task<CustomerWriteResult> Create(CreateCustomerRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim();

        // Email is unique at the database level (see CustomerConfiguration); the
        // pre-check keeps the happy path cheap, the catch keeps it correct.
        if (await db.Customers.AnyAsync(c => c.Email == email, ct))
        {
            return new CustomerWriteResult(CustomerWriteOutcome.Conflict, null, $"A customer with email '{email}' already exists.");
        }

        var customer = new CustomerEntity { Email = email, Active = request.Active };
        db.Customers.Add(customer);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return new CustomerWriteResult(CustomerWriteOutcome.Conflict, null, $"A customer with email '{email}' already exists.");
        }

        return new CustomerWriteResult(CustomerWriteOutcome.Succeeded, ToResponse(customer), null);
    }

    /// Activation flips with a single guarded UPDATE — the state is read and written
    /// in one statement, never read-check-then-write, so concurrent flips cannot race.
    public async Task<CustomerWriteResult> SetActive(int id, SetCustomerActiveRequest request, CancellationToken ct)
    {
        var affected = await db.Customers
            .Where(c => c.Id == id && c.Active != request.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Active, request.Active), ct);

        if (affected == 0)
        {
            // Either the customer is gone, or it is already in the requested state.
            var current = await Get(id, ct);
            return current is null
                ? new CustomerWriteResult(CustomerWriteOutcome.NotFound, null, $"Customer {id} was not found.")
                : new CustomerWriteResult(CustomerWriteOutcome.Succeeded, current, null);
        }

        return new CustomerWriteResult(CustomerWriteOutcome.Succeeded, await Get(id, ct), null);
    }

    private static CustomerResponse ToResponse(CustomerEntity c) => new(c.Id, c.Email, c.Active);
}
