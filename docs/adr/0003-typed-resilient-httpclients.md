# 3. Typed, resilient HttpClients for sync calls

- Status: accepted
- Date: scaffold time

## Context
Cross-context reads need a synchronous call. A bare `new HttpClient()` plus
hand-rolled retries leaks untyped JSON and inconsistent failure handling across
services.

## Decision
One typed `HttpClient` per call target, registered via `AddServiceDefaults`
(Aspire service discovery for the base address) with the **standard resilience
handler** (`Microsoft.Extensions.Http.Resilience` — timeout, retry, circuit
breaker) attached. Responses map to a **contract** DTO — never another
service's EF entity.

## Consequences
- Uniform, typed, testable cross-service reads.
- A slow downstream is bounded (timeout + breaker), not cascading.
- Writes are never done this way — they are sagas (ADR 0002).
