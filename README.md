# dotnet-monolith-shop — .NET / ASP.NET Core monolith fixture

A small but genuine ASP.NET Core (net10.0) monolith with real bounded-context
seams, for exercising **modernization-dotnet-monolith** end to end.

- Namespaces = candidate contexts: `Catalog`, `Customers`, `Inventory`,
  `Payments`, `Orders`.
- One **god `ShopDbContext`** owns every entity — the database-per-service seam.
- `OrderService.PlaceOrder` reaches into Catalog + Customers + Inventory +
  Payments and commits them in **one `SaveChanges`** — the CreateOrder saga seam.
- Deliberate legacy smells to fix on the way out: `InventoryService.Reserve`
  read-check-then-writes (oversell), `Payment.Status` is a bare string.

Decomposes to: catalog / customers / inventory / payments / orders services,
database-per-service, and the CreateOrder saga (reserve stock → charge payment).
