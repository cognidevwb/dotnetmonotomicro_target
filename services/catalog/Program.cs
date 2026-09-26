using Catalog;
using Catalog.Features.Category;
using Catalog.Features.Product;
using Catalog.Features.StockItem;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
opts.PersistMessagesWithPostgresql(builder.Configuration.GetConnectionString("catalogdb")!);
opts.UseEntityFrameworkCoreTransactions();             // SaveChanges + outgoing flush atomic
opts.Policies.AutoApplyTransactions();                 // wrap each handler in a transaction
opts.Policies.UseDurableInboxOnAllListeners();          // idempotent consumer (dedup)
opts.Policies.UseDurableOutboxOnAllSendingEndpoints();  // transactional outbox on every publisher
}
// Catalog owns no saga: its writes are single-context, so the HTTP slices below use the
// local DbContext unit of work. Wolverine stays wired for the outbox other contexts consume.
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

// This service's own store (database-per-service). The transactional outbox + durable
// inbox are already wired in the UseWolverine block above.
builder.Services.AddDbContext<CatalogDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("catalogdb")));

// Catalog is a leaf context: it reads nothing from another service, so there are no
// typed HttpClients here — other contexts call IN (orders reads /catalog/products/{id}/price).
builder.Services.AddScoped<CatalogService>();
builder.Services.AddScoped<CategoryHandler>();
builder.Services.AddScoped<ProductHandler>();
builder.Services.AddScoped<StockItemHandler>();

builder.Services.AddScoped<IValidator<CreateCategoryRequest>, CreateCategoryRequestValidator>();
builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
builder.Services.AddScoped<IValidator<ReserveStockRequest>, ReserveStockRequestValidator>();

var app = builder.Build();

app.UseExceptionHandler();
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.MapDefaultEndpoints();              // /health + /alive

// The catalog slices extracted from the monolith.
app.MapCategoryEndpoints(authEnabled);
app.MapProductEndpoints(authEnabled);
app.MapStockItemEndpoints(authEnabled);

app.Run();

// Exposed so the integration test project can host the app via WebApplicationFactory<Program>.
public partial class Program { }
