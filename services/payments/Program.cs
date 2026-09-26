using Microsoft.AspNetCore.Authentication.JwtBearer;
using Payments;
using Payments.Features.Payment;
using Payments.Infrastructure;
using Wolverine;   // UseWolverine + policies
using Wolverine.Postgresql;   // PersistMessagesWith* durable store extension

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();          // discovery + OpenTelemetry + resilience + health
builder.Services.AddProblemDetails();  // RFC 9457 error shape
builder.Host.UseWolverine(opts =>
{
// Transactional outbox (no dual-write) + durable inbox (idempotent, at-least-once).
if (builder.Configuration.GetValue("Messaging:Durable", true))
{
opts.PersistMessagesWithPostgresql(builder.Configuration.GetConnectionString("paymentsdb")!);
opts.UseEntityFrameworkCoreTransactions();             // SaveChanges + outgoing flush atomic
opts.Policies.AutoApplyTransactions();                 // wrap each handler in a transaction
opts.Policies.UseDurableInboxOnAllListeners();          // idempotent consumer (dedup)
opts.Policies.UseDurableOutboxOnAllSendingEndpoints();  // transactional outbox on every publisher
}
});

// Authentication — the token is re-validated inside the service (defence in
// depth: the gateway authenticates at the edge, each service authorizes).
var authEnabled = !string.IsNullOrWhiteSpace(builder.Configuration["Auth:Authority"]);
if (authEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Auth:Authority"];
            options.Audience = builder.Configuration["Auth:Audience"];
            options.RequireHttpsMetadata = builder.Environment.IsProduction();
        });
    builder.Services.AddAuthorization();
}

// This service's own store (database-per-service). Wolverine's EF Core transactions +
// outbox, wired above, enroll this same context in the handler.
builder.Services.AddDbContext<PaymentsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("paymentsdb")));

// payments owns its data outright and makes no cross-context calls, so there are no
// typed HttpClients to register here.
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<PaymentHandler>();
builder.Services.AddScoped<IValidator<ChargePaymentRequest>, ChargePaymentRequestValidator>();

var app = builder.Build();

app.UseExceptionHandler();
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.MapDefaultEndpoints();              // /health + /alive

var payments = app.MapPayment();
if (authEnabled)
{
    payments.RequireAuthorization();
}

app.Run();

// Exposed so the integration test project can host the app via WebApplicationFactory<Program>.
public partial class Program { }
