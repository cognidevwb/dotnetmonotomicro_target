# Orders

Part of the **Acmeplatform** platform decomposition. Bounded-context service extracted
from the monolith — owns its own schema (`ordersdb`), no shared DB, no
cross-service FK/JOIN.

## What this service does

Orders is where a customer's purchase becomes a commitment: it accepts a basket of
product lines, checks the customer is active, reserves the stock, prices each line
from the catalog, and records the resulting order and its lines as its own
authoritative data. Because the money movement is the hardest step to undo, the
service orchestrates the CreateOrder saga so the payment charge runs last, after
stock is reserved, and unwinds in reverse (refund → release stock → cancel order)
if anything fails. It also serves order lookup by id, exposing status as a stable
enum rather than the monolith's free-text column.

_Carved from: Orders/Order.cs, Orders/OrderLine.cs, Orders/OrderService.cs, Orders/OrdersController.cs._

## Ownership

- **Owned entities:** Order, OrderLine
- **Aggregate endpoints:** Order
- **Outbound calls:** [catalog](../catalog/README.md), [customers](../customers/README.md), [payments](../payments/README.md)
- **Saga participation:** orchestrates a saga — see `../../SEQUENCE-DIAGRAMS.md`

## Health

`/health` (readiness) and `/alive` (liveness) — wired by `ServiceDefaults` via
`app.MapDefaultEndpoints()` in `Program.cs`.

## Run & test

```bash
dotnet run --project services/orders
dotnet test services/orders.Tests
```
