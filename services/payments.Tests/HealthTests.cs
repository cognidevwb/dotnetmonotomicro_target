using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Payments.Tests;

// Hosts the real Payments app in-process (WebApplicationFactory) and proves it
// starts and reports healthy. `Messaging:Durable=false` skips the Wolverine message-store
// provisioning so this cheap smoke test needs no database; the per-slice tests the develop
// step adds spin up a real PostgreSQL via Testcontainers and exercise the outbox for real.
public sealed class HealthTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var client = factory
            .WithWebHostBuilder(b => b.UseSetting("Messaging:Durable", "false"))
            .CreateClient();
        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
