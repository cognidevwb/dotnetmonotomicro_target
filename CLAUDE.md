# CLAUDE.md — conventions for this platform

## Shape
Every service under `services/<dir>/` is a standalone ASP.NET Core project
referencing `ServiceDefaults`. Layout: `Program.cs`, `Domain/`, `Features/`,
`Infrastructure/Configurations/`, `Contracts/`, `Clients/`, `k8s/` (or a Helm
chart under `deploy/chart/`). Its integration tests live in the sibling
`services/<dir>.Tests/` project.

## The `CW-SEAM` marker convention
Scaffolded skeletons and generated docs carry a `CW-SEAM[kind=...]` marker
where the port/develop step fills real monolith logic — the comment syntax
matches the file it's in:
- `// CW-SEAM[kind=...]` in C#.
- `# CW-SEAM[kind=...]` in YAML (e.g. `deploy/strangler.values.yaml`'s
  per-context traffic weight).
- `<!-- CW-SEAM[kind=...] -->` in Markdown — `services/<ctx>/README.md`'s
  `business-capability` summary and this platform's `ARCHITECTURE.md`
  `decomposition-rationale` section.

When you finish a seam, **remove the marker** — a leftover `CW-SEAM`,
`TODO(cognidev)`, or "the develop step" placeholder means the work is
unfinished and the gate fails.

## Per-service ownership
- `services/catalog/` — the **Catalog** context (owns `catalogdb`).
- `services/customers/` — the **Customers** context (owns `customersdb`).
- `services/payments/` — the **Payments** context (owns `paymentsdb`).
- `services/orders/` — the **Orders** context (owns `ordersdb`).

## Non-negotiables
1. Database-per-service — no shared schema, no cross-service FK/JOIN/EF
   navigation property.
2. Cross-service writes are sagas with an EF Core transactional outbox.
3. Every service calls `builder.AddServiceDefaults()` — discovery,
   OpenTelemetry, resilience, health configured once, identically.
4. Typed, resilient `HttpClient`s only; async all the way.
5. `dotnet build Acmeplatform.slnx` and `dotnet test Acmeplatform.slnx` must both pass
   before a service ships.
