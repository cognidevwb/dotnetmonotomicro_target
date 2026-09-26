# Payments

Part of the **Acmeplatform** platform decomposition. Bounded-context service extracted
from the monolith — owns its own schema (`paymentsdb`), no shared DB, no
cross-service FK/JOIN.

## What this service does

Payments is the money-taking step of the order flow: it charges an order for a given
amount and records the resulting payment — its id, the order it settles, the amount and
where it sits in the payment lifecycle (pending, charged, refunded, failed). A charge is
idempotent per order — one payment per order, enforced by a unique index — so a retried
or duplicated request is rejected as a conflict rather than billing a customer twice.
Once a charge commits, the service publishes `PaymentCharged` through its transactional
outbox so the order saga can move on without a dual-write.

_Carved from: Payments/Payment.cs, Payments/PaymentService.cs._

## Ownership

- **Owned entities:** Payment
- **Aggregate endpoints:** Payment
- **Outbound calls:** none (synchronous only)
- **Saga participation:** participates in a saga / async messaging — see `../../SEQUENCE-DIAGRAMS.md`

## Health

`/health` (readiness) and `/alive` (liveness) — wired by `ServiceDefaults` via
`app.MapDefaultEndpoints()` in `Program.cs`.

## Run & test

```bash
dotnet run --project services/payments
dotnet test services/payments.Tests
```
