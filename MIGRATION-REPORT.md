# Migration report

## Summary

This migration decomposed the monolith into **4 microservice(s)** (catalog-service, customers-service, payments-service, orders-service). It moved **149** lines of business logic verbatim and generated **165** lines of platform code — **92%** produced deterministically. 1 saga(s) coordinate cross-service writes. 17/17 develop tasks completed cleanly.

## Services

- `catalog-service`
- `customers-service`
- `payments-service`
- `orders-service`

## Conversion statistics

| Metric | Value |
| --- | --- |
| Lines ported verbatim | 149 |
| Lines generated (deterministic) | 165 |
| Total lines | 314 |
| Deterministic share | 92% |
| LLM seam points | 9 |
| Sagas | 1 |

## Manual intervention required

None detected — every construct was ported or generated, every develop task closed cleanly, and no unfilled markers remain.
## Build proof

All **11** target(s) compile in Release (`dotnet build -c Release`).
