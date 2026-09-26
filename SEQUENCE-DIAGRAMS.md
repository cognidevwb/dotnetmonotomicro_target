# Sequence diagrams — orchestrated sagas

Each cross-service write is an orchestrated saga via Wolverine (handlers + EF outbox): the
hardest-to-reverse step runs **last** as the pivot; any earlier failure
compensates in reverse. Delivery is at-least-once, so every consumer/handler
is idempotent.

## CreateOrder (orchestrator: `orders`)

```mermaid
sequenceDiagram
  participant O as orders
  participant P0 as catalog
  participant P1 as customers
  participant P2 as payments
  Note over O: begin saga — write outbox row in the same EF Core transaction as state
  O->>P0: reserve (catalog)
  P0-->>O: reserved
  O->>P1: reserve (customers)
  P1-->>O: reserved
  O->>P2: reserve (payments)
  P2-->>O: reserved
  O->>O: commit the pivot step (hardest-to-reverse — LAST)
  Note over O: on any failure, compensate in reverse
  O-->>P2: compensate — release (payments)
  O-->>P1: compensate — release (customers)
  O-->>P0: compensate — release (catalog)
```

