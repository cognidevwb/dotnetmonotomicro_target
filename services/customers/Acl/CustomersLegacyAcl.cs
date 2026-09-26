#nullable enable
using CustomerEntity = Customers.Domain.Customer;

namespace Customers.Acl;

/// The customer row as the legacy monolith persisted it: CUST_* columns and a
/// stringly-typed status. Legacy naming stays quarantined in this file.
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Naming", "CA1707:Identifiers should not contain underscores",
    Justification = "Mirrors the legacy monolith's column names verbatim; quarantined to the ACL.")]
public sealed record LegacyCustomerRow(int CUST_ID, string? CUST_EMAIL, string? CUST_STATUS);

/// Pure translation of legacy shapes into the Customers domain model — no EF, no HTTP.
public static class CustomersLegacyAcl
{
    private static readonly HashSet<string> ActiveStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "A", "ACTIVE", "1", "Y", "TRUE" };

    private static readonly HashSet<string> InactiveStatuses =
        new(StringComparer.OrdinalIgnoreCase) { "I", "INACTIVE", "C", "CLOSED", "0", "N", "FALSE" };

    /// Translate a legacy row into a domain customer, rejecting shapes that
    /// violate a domain invariant at the boundary.
    public static CustomerEntity ToDomain(LegacyCustomerRow row)
    {
        ArgumentNullException.ThrowIfNull(row);

        if (row.CUST_ID <= 0)
        {
            throw new LegacyTranslationException($"Legacy customer id '{row.CUST_ID}' is not a valid identity.");
        }

        var email = row.CUST_EMAIL?.Trim() ?? "";
        if (email.Length == 0 || !email.Contains('@', StringComparison.Ordinal))
        {
            throw new LegacyTranslationException($"Legacy customer {row.CUST_ID} has no usable email.");
        }

        return new CustomerEntity
        {
            Id = row.CUST_ID,
            Email = email,
            Active = ToActive(row.CUST_STATUS),
        };
    }

    /// Coerce the legacy stringly-typed status onto the domain's Active flag.
    public static bool ToActive(string? legacyStatus)
    {
        var status = legacyStatus?.Trim();
        if (string.IsNullOrEmpty(status))
        {
            throw new LegacyTranslationException("Legacy customer status is missing.");
        }

        if (ActiveStatuses.Contains(status))
        {
            return true;
        }

        if (InactiveStatuses.Contains(status))
        {
            return false;
        }

        throw new LegacyTranslationException($"Unmapped legacy customer status '{status}'.");
    }

    /// Project a domain customer back onto the legacy row shape (dual-write during cutover).
    public static LegacyCustomerRow ToLegacy(CustomerEntity customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        return new LegacyCustomerRow(customer.Id, customer.Email, customer.Active ? "ACTIVE" : "INACTIVE");
    }
}

/// Raised when a legacy shape cannot be translated without violating a domain invariant.
public sealed class LegacyTranslationException(string message) : Exception(message);
