# Catalog

Part of the **Acmeplatform** platform decomposition. Bounded-context service extracted
from the monolith — owns its own schema (`catalogdb`), no shared DB, no
cross-service FK/JOIN.

## What this service does

Catalog is the system of record for what the business sells: the category taxonomy,
the products hanging off it, and each product's price. It answers the product and
price lookups other contexts depend on — orders reads `/catalog/products/{id}/price`
through its typed client rather than joining across a shared database.

It also owns on-hand stock for those products, exposing the stock level per product
and the reservation that decrements it. The reservation is a single guarded update
carrying the "enough stock" invariant in its WHERE clause, so concurrent orders
cannot oversell the way the monolith's read-then-write could.

_Carved from: Catalog/CatalogController.cs, Catalog/CatalogService.cs, Catalog/Category.cs, Catalog/Product.cs, Inventory/InventoryService.cs, Inventory/StockItem.cs._

## Ownership

- **Owned entities:** Category, Product, StockItem
- **Aggregate endpoints:** Category, Product, StockItem
- **Outbound calls:** none (synchronous only)
- **Saga participation:** participates in a saga / async messaging — see `../../SEQUENCE-DIAGRAMS.md`

## Health

`/health` (readiness) and `/alive` (liveness) — wired by `ServiceDefaults` via
`app.MapDefaultEndpoints()` in `Program.cs`.

## Run & test

```bash
dotnet run --project services/catalog
dotnet test services/catalog.Tests
```
