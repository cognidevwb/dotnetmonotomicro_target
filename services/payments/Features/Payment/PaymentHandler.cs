#nullable enable
using Payments.Acl;
using Payments.Infrastructure;
using PaymentEntity = Payments.Domain.Payment;

namespace Payments.Features.Payment;

/// Published after a successful charge — written through the transactional outbox in the
/// same transaction as the payment row, so there is no dual-write.
public sealed record PaymentCharged(int PaymentId, int OrderId, decimal Amount);

/// Raised when an order already has a payment — the caller sees 409, not a double charge.
public sealed class PaymentAlreadyExistsException(int orderId)
    : Exception($"Order {orderId} has already been charged.")
{
    public int OrderId { get; } = orderId;
}

/// The Payment slice handler — the business rule extracted from the monolith's
/// PaymentService.Charge, with the charge made idempotent per order.
public sealed class PaymentHandler(PaymentsDbContext db, IDbContextOutbox outbox)
{
    public async Task<PaymentResponse> ChargeAsync(ChargePaymentRequest request, CancellationToken ct)
    {
        // Idempotency is enforced by the unique index on order_id, not by this read:
        // the read is a fast path, the DbUpdateException below is the real guard, so a
        // concurrent duplicate charge loses at the database rather than in memory.
        var existing = await db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == request.OrderId, ct);

        if (existing is not null)
        {
            throw new PaymentAlreadyExistsException(request.OrderId);
        }

        var payment = new PaymentEntity
        {
            OrderId = request.OrderId,
            Amount = request.Amount,
            Status = PaymentsLegacyAcl.ToLegacyStatus(PaymentStatus.Charged)
        };

        db.Payments.Add(payment);

        outbox.Enroll(db);
        await outbox.PublishAsync(new PaymentCharged(payment.Id, payment.OrderId, payment.Amount));

        try
        {
            // Persists the payment and flushes the outgoing message atomically.
            await outbox.SaveChangesAndFlushMessagesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw new PaymentAlreadyExistsException(request.OrderId);
        }

        return ToResponse(payment);
    }

    public async Task<PaymentResponse?> GetAsync(int id, CancellationToken ct)
    {
        var payment = await db.Payments.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        return payment is null ? null : ToResponse(payment);
    }

    public async Task<PaymentResponse?> GetByOrderAsync(int orderId, CancellationToken ct)
    {
        var payment = await db.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.OrderId == orderId, ct);
        return payment is null ? null : ToResponse(payment);
    }

    private static PaymentResponse ToResponse(PaymentEntity payment) =>
        new(payment.Id, payment.OrderId, payment.Amount, PaymentsLegacyAcl.StatusOf(payment));
}
