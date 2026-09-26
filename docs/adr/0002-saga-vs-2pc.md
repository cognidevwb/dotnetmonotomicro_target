# 2. Sagas over two-phase commit

- Status: accepted
- Date: scaffold time

## Context
With database-per-service there is no distributed transaction to span contexts.
Two-phase commit across databases is slow, couples availability, and is
unsupported across our data stores.

## Decision
Cross-service writes are **orchestrated sagas** via Wolverine. The
hardest-to-reverse step runs **last** as the pivot; any earlier failure
compensates in reverse. Messaging over RabbitMQ.

## Consequences
- Eventual consistency between contexts; the UI accounts for in-flight state.
- Every step needs a compensating action.
- No global lock — availability and throughput improve.
