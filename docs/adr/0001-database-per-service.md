# 1. Database-per-service

- Status: accepted
- Date: scaffold time

## Context
A shared monolith database couples every context: a schema change ripples across
teams, and cross-service JOINs make independent deploy impossible.

## Decision
Each service **owns its schema** (PostgreSQL, one logical database per service). No
shared DB, no cross-service FK, no cross-service JOIN or EF navigation property.
A service reads another's data via its API or a replicated read model.

## Consequences
- Independent deploy + scaling per service.
- Cross-context consistency needs sagas + an outbox (see ADR 0002/0004), not FKs.
- Some data is duplicated into read models — accepted for decoupling.
