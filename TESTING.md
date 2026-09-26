# Testing

Every service ships one **first-class** integration proof today and grows
per-slice tests as the port step fills business code.

- **`HealthTests.cs`** (scaffolded) — hosts the real app in-process via
  `WebApplicationFactory<Program>` and asserts `GET /health` returns 200. This
  is the "it builds and starts" proof the verify step aggregates.
- **Per-slice tests** (added by the port step) — xUnit v3 + Testcontainers (a
  real database, a real broker where used) + `WebApplicationFactory` against
  the real app: happy path, validation → 400, not-found → 404, and the
  concurrency / out-of-stock guard.
- **Saga tests** (orchestrator services) — the happy path commits across
  participants; a failure at the pivot step triggers compensation in reverse.
- **Contract boundaries** (`Contracts/ContractBoundaries.cs` +
  `_support/SchemaGuard.cs`) — every outbound call target has a
  `Contracts/fixtures/<provider>/*.json` directory of recorded responses; a
  breaking shape change to a provider fails THIS service's build
  (consumer-driven contracts, no PactNet dependency).
- **Behavioral equivalence** (`_support/GoldenReplay.cs` +
  `_support/JsonNormalize.cs` + `Equivalence/fixtures/<aggregate>/*.json`) —
  replays a `{ request, seed, expectedResponse }` golden captured from the
  legacy monolith against the new service and asserts the normalized response
  matches, proving new == legacy behavior per aggregate.

```bash
dotnet test Acmeplatform.slnx           # the whole platform
dotnet test services/<ctx>.Tests # one service
```
