# Security

## AuthN / AuthZ — a generic OIDC provider (Keycloak / Auth0 / Okta)
- **JWT bearer.** `Microsoft.AspNetCore.Authentication.JwtBearer` validates the token against `Auth:Authority` / `Auth:Audience`. The YARP gateway validates the token once at the
  edge; each service **re-validates** it too (defence in depth), so a
  compromised or misrouted internal call still needs a valid token.
- Service-to-service: prefer a client-credentials token or mTLS. Never trust
  an unauthenticated internal caller.

## Input & error handling
- Request DTOs are validated (data annotations / FluentValidation) at the edge
  before the handler runs.
- All errors return **RFC 9457 `application/problem+json`**
  (`AddProblemDetails()` + `UseExceptionHandler()`) — no stack traces, no
  internal detail leaks to the client.

## Data & transport
- **Database-per-service** (PostgreSQL): a compromised service cannot read
  another's tables — there is no shared schema, connection string, or FK.
- Secrets via a k8s `Secret` / Azure Key Vault, never in images or a committed
  `appsettings.json`.
- TLS terminates at the gateway; internal traffic stays on the cluster network
  (mTLS where required).

## Supply chain
- Central Package Management (`Directory.Packages.props`) pins every version
  in one place. CI runs a vulnerability scan (Trivy/Grype) and emits an SBOM
  (CycloneDX/Syft); pin third-party CI actions by SHA.

## Observability as a control
- OpenTelemetry → OTLP → Grafana LGTM carries the auth subject on every trace (never secrets) for
  audit and anomaly detection.
