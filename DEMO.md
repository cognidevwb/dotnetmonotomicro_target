# Demo — .NET monolith → microservices

**Playbook:** `modernization-dotnet-monolith` · **Engine model:** Claude Opus 4.8 · **Result:** ✅ 17/17 tasks · all 11 projects compile in Release

A single ASP.NET Core (net10) monolith decomposed into **4 bounded-context microservices** on a
.NET Aspire platform — database-per-service, a gateway, typed resilient HTTP clients, and a saga
for the one cross-service write. Real, unedited run.

## Before → after

| | Before — `fixtures/dotnet-monolith-shop` | After — this folder |
|---|---|---|
| Shape | one deployable monolith | 4 services + gateway on .NET Aspire |
| Services | — | `catalog`, `customers`, `orders`, `payments` (each with a `.Tests` project) |
| Data | one shared schema | database-per-service |
| Cross-service **writes** | in-process calls | **1 saga** (see `SEQUENCE-DIAGRAMS.md`) |
| Cross-service **reads** | method calls | typed, resilient `HttpClient`s |
| Orchestration | — | Aspire `AppHost` + shared `ServiceDefaults` |
| Delivery | — | `docker-compose.yml`, `azure.yaml`, `Makefile` |
| Build | — | **all 11 targets compile in Release** (`Acmeplatform.slnx`) |

The playbook moved **149 lines of business logic verbatim** and generated **165 lines** of
platform code — **92 % produced deterministically**, with Opus filling only 9 seam points.

## What to look at

- **`ARCHITECTURE.md`** — bounded-context map (mermaid), database-per-service + saga rules.
- **`SEQUENCE-DIAGRAMS.md`** — the cross-service saga, step by step.
- **`services/`** — the four services; each owns its schema and tests.
- **`AppHost/`** — the Aspire orchestrator wiring services + gateway together.
- **`MIGRATION-REPORT.md`** — ported-vs-generated stats + the build proof.

## Open it in the workbench

Open this folder (`demos/dotnet-monolith-to-microservices/`). The Explorer shows the Aspire
layout (`AppHost`, `gateway`, `ServiceDefaults`, `services/*`), the architecture docs, and the
`.cognidev/` analysis — the structural intelligence and decomposition plan the run produced.

> Build it yourself with the net10 SDK: `dotnet build Acmeplatform.slnx -c Release` (0 errors).
