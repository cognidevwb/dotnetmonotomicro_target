# Performance

## Async everywhere
ASP.NET Core Minimal APIs (or controllers) over Kestrel; EF Core `async`/
`await` end to end. Never a blocking `.Result`/`.Wait()` in the request path —
one synchronous call stalls a thread-pool thread under load.

## Connection pooling
EF Core pools connections via the ADO.NET provider (Npgsql /
Microsoft.Data.SqlClient); size the pool to the service's own database, and
use `AddDbContextPool` where the context is stateless enough to reuse.

## Sync fan-out
Typed `HttpClient`s via Aspire service discovery reuse connections
(`IHttpClientFactory`), with the standard resilience handler (timeout + retry
+ circuit breaker) from `Microsoft.Extensions.Http.Resilience` attached. A
cross-service **write** is a saga, so the request path never blocks on a
second service's transaction.

## Read models
Where read load justifies it, keep a replicated read model (CQRS) instead of
a synchronous cross-context read on the hot path.

## Budgets & proof
- Per-service p99 latency + error-rate SLOs (OpenTelemetry → Grafana LGTM /
  Azure Monitor).
- Load-test the extracted context against the monolith baseline before
  cutover.
- Watch outbox lag and DB pool saturation as the leading indicators.
