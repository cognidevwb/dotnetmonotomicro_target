using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();   // discovery + OpenTelemetry + resilience + health

// Strangler-fig: route not-yet-extracted paths to the legacy monolith. Set
// LEGACY_UPSTREAM per environment; when unset the appsettings default stands.
var legacyUpstream = builder.Configuration["LEGACY_UPSTREAM"];
if (!string.IsNullOrWhiteSpace(legacyUpstream))
{
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ReverseProxy:Clusters:legacy-monolith:Destinations:d1:Address"] = legacyUpstream,
    });
}

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();   // logical names → live service endpoints

// Edge authentication — the token is validated once at the gateway, then
// re-validated per service (defence in depth). Only activates when the identity
// provider is configured, so `dotnet run` works locally without one.
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

var app = builder.Build();

app.MapDefaultEndpoints();
if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}
app.MapReverseProxy();
app.Run();
