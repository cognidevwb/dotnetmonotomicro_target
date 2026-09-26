# 4. Transactional outbox for every cross-service write

- Status: accepted
- Date: scaffold time

## Context
Publishing an event after committing a DB write is a dual-write: a crash
between the two loses the event. At-least-once delivery must not lose
messages.

## Decision
Persist the domain change and the outgoing message in the **same** EF Core
transaction (the outbox provider the messaging library wires in). An async
relay publishes outbox rows and marks them sent. Consumers are **idempotent**
(dedup by message id).

## Consequences
- No lost events, no phantom events.
- A small relay/dispatcher + an outbox table per publishing service.
- Consumers must tolerate duplicates.
