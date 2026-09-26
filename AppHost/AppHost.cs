// .NET Aspire distributed app model — `aspire run` locally (whole system + telemetry),
// `aspire deploy` to Azure Container Apps (managed identity, passwordless connections).
// Azure resources run as local containers in dev.
var builder = DistributedApplication.CreateBuilder(args);

// Azure Container Apps is the compute target on publish.
builder.AddAzureContainerAppEnvironment("cae");

// Observability: OpenTelemetry over OTLP. The Aspire dashboard collects it in
// dev; the OTel Collector → Grafana LGTM does in compose/k8s (OTEL_EXPORTER_OTLP_ENDPOINT).

// Data: Azure PostgreSQL Flexible Server (real on publish, local container in dev).
var postgres = builder.AddAzurePostgresFlexibleServer("postgres").RunAsContainer();
// Messaging: RabbitMQ (local container; a managed broker in prod).
var messaging = builder.AddRabbitMQ("messaging");

var catalogDb = postgres.AddDatabase("catalogdb");
builder.AddProject<Projects.Catalog>("catalog")
    .WithReference(catalogDb)
    .WithReference(messaging);

var customersDb = postgres.AddDatabase("customersdb");
builder.AddProject<Projects.Customers>("customers")
    .WithReference(customersDb)
    .WithReference(messaging);

var paymentsDb = postgres.AddDatabase("paymentsdb");
builder.AddProject<Projects.Payments>("payments")
    .WithReference(paymentsDb)
    .WithReference(messaging);

var ordersDb = postgres.AddDatabase("ordersdb");
builder.AddProject<Projects.Orders>("orders")
    .WithReference(ordersDb)
    .WithReference(messaging);

builder.Build().Run();
