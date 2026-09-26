# Customers

Part of the **Acmeplatform** platform decomposition. Bounded-context service extracted
from the monolith — owns its own schema (`customersdb`), no shared DB, no
cross-service FK/JOIN.

## What this service does

Customers is the platform's system of record for who may buy: it registers a
customer against a unique email address and tracks whether that account is
currently active. It serves customer lookups (`GET /customers/{id}`), registration
(`POST /customers`) and activation changes (`PUT /customers/{id}/active`), keeping
the activation flip atomic so concurrent changes cannot race.

It also exposes the `GET /customers/{id}/active` probe that the orders service
calls before accepting an order — the one cross-context question other services
ask of this context. It owns no other data and calls no other service.

_Carved from: Customers/Customer.cs, Customers/CustomerService.cs, Customers/CustomersController.cs._

## Ownership

- **Owned entities:** Customer
- **Aggregate endpoints:** Customer
- **Outbound calls:** none (synchronous only)
- **Saga participation:** participates in a saga / async messaging — see `../../SEQUENCE-DIAGRAMS.md`

## Health

`/health` (readiness) and `/alive` (liveness) — wired by `ServiceDefaults` via
`app.MapDefaultEndpoints()` in `Program.cs`.

## Run & test

```bash
dotnet run --project services/customers
dotnet test services/customers.Tests
```
