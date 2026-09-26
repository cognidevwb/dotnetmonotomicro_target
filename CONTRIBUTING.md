# Contributing

## Setup
```bash
dotnet restore Acmeplatform.slnx
```
this checkout holds every service permanently (`single-repo`) — the `.slnx` is the source of truth.

## Run
```bash
dotnet run --project AppHost/AppHost.csproj    # Aspire — whole platform, one command
dotnet run --project services/<ctx>            # a single service
```

## Before you push
```bash
dotnet format --verify-no-changes           # style
dotnet build Acmeplatform.slnx -c Release           # -warnaserror where the CI enforces it
dotnet test Acmeplatform.slnx
```

## Conventions
- Vertical-slice per feature under `Domain/` / `Features/` / `Infrastructure/`
  / `Contracts/` / `Clients/` — no layered `Controllers/Services/Repositories`
  split across the whole service.
- Async all the way — EF Core `async`/`await`, no `.Result`/`.Wait()` in the
  request path.
- A cross-service write is a **saga with a transactional outbox**, never a
  chained sync call.
- Typed, resilient `HttpClient`s only (Aspire service discovery + the standard
  resilience handler) — no bare `new HttpClient()`.
- Keep files small and single-purpose; see `CLAUDE.md`.
