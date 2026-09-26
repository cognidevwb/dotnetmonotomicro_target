# 5. Strangler-fig cutover order

- Status: accepted
- Date: scaffold time

## Context
A big-bang rewrite is high-risk. The monolith must keep serving while
contexts are extracted one at a time; auth (a generic OIDC provider (Keycloak/Auth0/Okta)) and routing must stay
consistent across both worlds during the transition.

## Decision
The YARP gateway routes `/<context>/**` to the new service; everything else
still hits the legacy monolith. Cut over in **strangler order**
(`plan.json#order`): leaf contexts (fewest outbound calls) first, the hub
last. Each cutover is independently verifiable and reversible at the gateway
(see `RUNBOOK.md`).

## Consequences
- Incremental, low-risk migration; rollback is a gateway route change.
- The gateway is a critical component — it is the cutover control plane.
- Both systems run in parallel until the last context is extracted.
