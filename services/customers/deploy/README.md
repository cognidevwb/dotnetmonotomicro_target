# Customers — strangler cutover runbook

This service is extracted from the monolith under the **strangler-fig** pattern: the
YARP gateway can send a slice of `customers` traffic here while the rest still hits the
legacy monolith, so you cut over incrementally with a fast rollback.

## Cut a slice of traffic over
1. Edit `deploy/strangler.values.yaml` → `strangler.customers.weight` (0..100).
2. Redeploy the gateway (or the umbrella): `helm upgrade --install platform deploy/chart`.
3. Watch this service's dashboards + the gateway error rate.

## Roll back instantly
- Set `weight: 0` and redeploy — all `customers` traffic returns to the monolith.
- Or `kubectl rollout undo deployment/customers` / scale to zero.

## Source of truth during cutover
The monolith remains authoritative for `customers` until `weight: 100` **and** its data has
been migrated. `legacyPaths` lists routes that must keep hitting the monolith even at
high weights (e.g. a report the new service does not yet serve).
