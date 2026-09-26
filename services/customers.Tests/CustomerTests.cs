using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Customers.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;
using CustomerResponse = Customers.Features.Customer.CustomerResponse;

namespace Customers.Tests;

/// Hosts the real Customers app against a real PostgreSQL (Testcontainers) so the
/// unique-email index and the guarded activation UPDATE are exercised for real.
/// Without a container runtime every test SKIPS rather than failing the suite.
public sealed class CustomersApp : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().Build();
    private WebApplicationFactory<Program>? _factory;

    public string? Unavailable { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _db.StartAsync();
        }
        catch (Exception ex)
        {
            Unavailable = $"no container runtime available: {ex.Message}";
            return;
        }

        // Schema first: the Wolverine message store provisions its own tables when the
        // app boots, and EnsureCreated is a no-op once any table exists.
        await using (var seed = NewDbContext())
        {
            await seed.Database.EnsureCreatedAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:customersdb", _db.GetConnectionString()));
    }

    public HttpClient Client => _factory!.CreateClient();

    public CustomersDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<CustomersDbContext>()
            .UseNpgsql(_db.GetConnectionString())
            .Options);

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _db.DisposeAsync();
    }
}

[CollectionDefinition(CustomersCollection.Name)]
public sealed class CustomersCollection : ICollectionFixture<CustomersApp>
{
    public const string Name = "customers-app";
}

internal static class CustomersJson
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}

[Collection(CustomersCollection.Name)]
public sealed class CustomerTests(CustomersApp app)
{
    private static int _seq;

    [Fact]
    public async Task Register_then_get_round_trips_the_customer()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var email = NextEmail();

        var created = await client.PostAsJsonAsync("/customers/", new { email, active = true });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var body = await created.Content.ReadFromJsonAsync<CustomerResponse>(CustomersJson.Web);
        Assert.NotNull(body);
        Assert.Equal(email, body!.Email);
        Assert.True(body.Active);

        var fetched = await client.GetAsync($"/customers/{body.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(body, await fetched.Content.ReadFromJsonAsync<CustomerResponse>(CustomersJson.Web));
    }

    /// The one cross-context question other services ask of this context.
    [Fact]
    public async Task Activity_probe_reports_the_current_state()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var id = await Register(client, active: true);

        Assert.True(await client.GetFromJsonAsync<bool>($"/customers/{id}/active", CustomersJson.Web));

        var deactivated = await client.PutAsJsonAsync($"/customers/{id}/active", new { active = false });
        Assert.Equal(HttpStatusCode.OK, deactivated.StatusCode);

        Assert.False(await client.GetFromJsonAsync<bool>($"/customers/{id}/active", CustomersJson.Web));
    }

    /// SetActive is a single guarded UPDATE, so re-applying the state it is already in
    /// is a no-op success rather than a lost update or a spurious 404.
    [Fact]
    public async Task Setting_the_state_it_already_has_is_idempotent()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var id = await Register(client, active: true);

        var response = await client.PutAsJsonAsync($"/customers/{id}/active", new { active = true });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(CustomersJson.Web);
        Assert.True(body!.Active);
    }

    /// Two concurrent flips of the same row must agree on one final state — the
    /// guarded UPDATE decides it at the database, not in memory.
    [Fact]
    public async Task Concurrent_activation_flips_converge()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var id = await Register(client, active: true);

        var responses = await Task.WhenAll(
            client.PutAsJsonAsync($"/customers/{id}/active", new { active = false }),
            client.PutAsJsonAsync($"/customers/{id}/active", new { active = false }));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.False(await client.GetFromJsonAsync<bool>($"/customers/{id}/active", CustomersJson.Web));
    }

    [Fact]
    public async Task Registering_a_duplicate_email_is_a_conflict()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var email = NextEmail();

        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/customers/", new { email, active = true })).StatusCode);

        var duplicate = await client.PostAsJsonAsync("/customers/", new { email, active = true });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task Registering_with_an_invalid_email_is_rejected(string email)
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        var response = await app.Client.PostAsJsonAsync("/customers/", new { email, active = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Registering_with_an_overlong_email_is_rejected()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        var response = await app.Client.PostAsJsonAsync(
            "/customers/", new { email = new string('a', 250) + "@example.com", active = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_customer_is_not_found()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/customers/987654")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/customers/987654/active")).StatusCode);

        var update = await client.PutAsJsonAsync("/customers/987654/active", new { active = false });
        Assert.Equal(HttpStatusCode.NotFound, update.StatusCode);
    }

    private static string NextEmail() => $"customer{Interlocked.Increment(ref _seq)}@example.com";

    private static async Task<int> Register(HttpClient client, bool active)
    {
        var response = await client.PostAsJsonAsync("/customers/", new { email = NextEmail(), active });
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(CustomersJson.Web);
        return body!.Id;
    }
}
