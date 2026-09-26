using Microsoft.AspNetCore.Authentication.JwtBearer;
using Wolverine;   // UseWolverine + policies
using Wolverine.Postgresql;   // PersistMessagesWith* durable store extension
using Customers;
using Customers.Features.Customer;
using Customers.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();          // discovery + OpenTelemetry + resilience + health
builder.Services.AddProblemDetails();  // RFC 9457 error shape
builder.Host.UseWolverine(opts =>
{
// Transactional outbox (no dual-write) + durable inbox (idempotent, at-least-once).
if (builder.Configuration.GetValue("Messaging:Durable", true))
{
opts.PersistMessagesWithPostgresql(builder.Configuration.GetConnectionString("customersdb")!);
opts.UseEntityFrameworkCoreTransactions();             // SaveChanges + outgoing flush atomic
opts.Policies.AutoApplyTransactions();                 // wrap each handler in a transaction
opts.Policies.UseDurableInboxOnAllListeners();          // idempotent consumer (dedup)
opts.Policies.UseDurableOutboxOnAllSendingEndpoints();  // transactional outbox on every publisher
}
// Customers publishes no cross-context commands of its own; the durable
// inbox above makes it a safe consumer if a saga later routes work here.
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

// This service's own database (database-per-service).
builder.Services.AddDbContext<CustomersDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("customersdb")));

// Customers is a leaf context — it owns Customer and calls no other service, so
// there are no typed cross-context HttpClients to register here.
builder.Services.AddScoped<CustomerHandler>();
builder.Services.AddScoped<CustomerService>();
builder.Services.AddScoped<IValidator<CreateCustomerRequest>, CreateCustomerRequestValidator>();

var app = builder.Build();

app.UseExceptionHandler();
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.MapDefaultEndpoints();              // /health + /alive

var customers = app.MapCustomer();
if (authEnabled)
{
    // Only enforced when an authority is configured; local/dev runs stay open.
    customers.RequireAuthorization();
}

app.Run();

// Exposed so the integration test project can host the app via WebApplicationFactory<Program>.
public partial class Program { }
