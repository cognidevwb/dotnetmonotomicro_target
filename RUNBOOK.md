# Runbook

## Health
| Service | Endpoints (via gateway) | Owns |
|---|---|---|
| Catalog | `/catalog/health`, `/catalog/alive` | `catalogdb` |
| Customers | `/customers/health`, `/customers/alive` | `customersdb` |
| Payments | `/payments/health`, `/payments/alive` | `paymentsdb` |
| Orders | `/orders/health`, `/orders/alive` | `ordersdb` |

Kubernetes probes hit `/health` (readiness) and `/alive` (liveness) on the
container port; `app.MapDefaultEndpoints()` (`ServiceDefaults`) wires both.

## Delivery format: `single-repo`
All services stay in **one repository** through cutover — `cutover-dotnet` produces a clean `microservices/main` branch, no repo split.

## Rollback to the monolith (strangler safety net)
The YARP gateway is the cutover point. To roll a context back:
1. In `gateway/appsettings.json`, point the `/<ctx>/**` route's cluster
   destination back at `legacy-monolith` (or remove the route so the catch-all
   falls through to it).
2. `kubectl rollout undo deployment/<dir>` to revert the service image, or
   scale it to zero: `kubectl scale deployment/<dir> --replicas=0`.
3. The monolith still owns the source of truth until the context is fully cut
   over — no data migration is required to roll back.

## Common incidents
- **Service won't start** — check `dotnet build services/<dir>` locally; a
  missing `ConnectionStrings:<ctx>db`, an unregistered `DbContext`, or a
  pending EF Core migration is the usual cause.
- **DB connection errors** — verify the `<ctx>db` connection string and that
  the per-service database is reachable; each service has its OWN database.
- **Saga stuck** — inspect the EF Core outbox table; delivery is
  at-least-once, so a stuck row means the outbox relay/dispatcher is down, not
  lost data.

## Alerts
- p99 latency / error-rate per service (OpenTelemetry → OTLP → Grafana LGTM or
  Azure Monitor).
- Outbox lag (rows unpublished past threshold).
- Pod restart loops (`/alive` liveness probe failing) and HPA thrash.
