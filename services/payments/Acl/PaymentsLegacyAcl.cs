#nullable enable
using PaymentEntity = Payments.Domain.Payment;

namespace Payments.Acl;

/// The payment lifecycle as a closed set — the legacy monolith persisted this as a
/// free-text column ("pending"/"charged"/…); Domain code only ever sees this enum.
public enum PaymentStatus
{
    Pending,
    Charged,
    Refunded,
    Failed
}

/// Legacy shape as it comes off the monolith's persistence/DTOs. Quarantined here so
/// no legacy naming or stringly-typed status leaks into Domain/.
public sealed record LegacyPaymentRow(int Id, int OrderId, decimal Amount, string? Status);

/// Anti-corruption layer for the payments context: pure translation, no EF and no HTTP.
public static class PaymentsLegacyAcl
{
    /// Coerce the monolith's free-text status onto the domain enum.
    /// Unknown/blank values are a boundary violation — we do not silently default them.
    public static PaymentStatus ToStatus(string? legacyStatus) =>
        legacyStatus?.Trim().ToLowerInvariant() switch
        {
            "pending" => PaymentStatus.Pending,
            "charged" or "captured" or "paid" => PaymentStatus.Charged,
            "refunded" => PaymentStatus.Refunded,
            "failed" or "declined" => PaymentStatus.Failed,
            _ => throw new InvalidLegacyPaymentException(
                $"Unrecognised legacy payment status '{legacyStatus}'.")
        };

    /// The canonical spelling written back to the legacy column.
    public static string ToLegacyStatus(PaymentStatus status) => status switch
    {
        PaymentStatus.Pending => "pending",
        PaymentStatus.Charged => "charged",
        PaymentStatus.Refunded => "refunded",
        PaymentStatus.Failed => "failed",
        _ => throw new InvalidLegacyPaymentException($"Unmappable status '{status}'.")
    };

    /// Translate a legacy row into the domain entity, rejecting shapes that violate a
    /// domain invariant at the boundary rather than letting them into the aggregate.
    public static PaymentEntity ToDomain(LegacyPaymentRow row)
    {
        if (row.OrderId <= 0)
        {
            throw new InvalidLegacyPaymentException(
                $"Legacy payment {row.Id} has no valid order reference ({row.OrderId}).");
        }

        if (row.Amount < 0m)
        {
            throw new InvalidLegacyPaymentException(
                $"Legacy payment {row.Id} has a negative amount ({row.Amount}).");
        }

        // Validate the status even though we persist the canonical string: an
        // unmappable legacy value must fail here, not downstream.
        var status = ToStatus(row.Status);

        return new PaymentEntity
        {
            Id = row.Id,
            OrderId = row.OrderId,
            Amount = row.Amount,
            Status = ToLegacyStatus(status)
        };
    }

    /// Read the domain entity's persisted status as the enum.
    public static PaymentStatus StatusOf(PaymentEntity payment) => ToStatus(payment.Status);
}

public sealed class InvalidLegacyPaymentException(string message) : Exception(message);
