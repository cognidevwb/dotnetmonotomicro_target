using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Payments.Acl;
using Payments.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;
using PaymentResponse = Payments.Features.Payment.PaymentResponse;

namespace Payments.Tests;

/// Hosts the real Payments app against a real PostgreSQL (Testcontainers): the charge
/// path writes through the Wolverine EF outbox, so an in-memory provider would not
/// exercise what actually runs. Without a container runtime every test SKIPS.
public sealed class PaymentsApp : IAsyncLifetime
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
            b.UseSetting("ConnectionStrings:paymentsdb", _db.GetConnectionString()));
    }

    public HttpClient Client => _factory!.CreateClient();

    public PaymentsDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<PaymentsDbContext>()
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

[CollectionDefinition(PaymentsCollection.Name)]
public sealed class PaymentsCollection : ICollectionFixture<PaymentsApp>
{
    public const string Name = "payments-app";
}

internal static class PaymentsJson
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}

[Collection(PaymentsCollection.Name)]
public sealed class PaymentTests(PaymentsApp app)
{
    // A distinct order id per test keeps the per-order idempotency guard meaningful.
    private static int _nextOrderId = 700_000;

    [Fact]
    public async Task Charge_records_the_payment_and_returns_it()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var orderId = NextOrderId();

        var response = await client.PostAsJsonAsync("/payments/charge", new { orderId, amount = 42.50m });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(PaymentsJson.Web);
        Assert.NotNull(body);
        Assert.Equal(orderId, body!.OrderId);
        Assert.Equal(42.50m, body.Amount);
        Assert.Equal(PaymentStatus.Charged, body.Status);
        Assert.True(body.Id > 0);

        var fetched = await client.GetAsync($"/payments/{body.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(body, await fetched.Content.ReadFromJsonAsync<PaymentResponse>(PaymentsJson.Web));
    }

    /// The lookup the order saga uses to reconcile a charge it may have retried.
    [Fact]
    public async Task Payment_is_addressable_by_its_order()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var orderId = NextOrderId();

        var created = await client.PostAsJsonAsync("/payments/charge", new { orderId, amount = 12m });
        var payment = await created.Content.ReadFromJsonAsync<PaymentResponse>(PaymentsJson.Web);

        var byOrder = await client.GetFromJsonAsync<PaymentResponse>(
            $"/payments/by-order/{orderId}", PaymentsJson.Web);

        Assert.Equal(payment, byOrder);
    }

    /// One payment per order: a retried charge must be refused, not bill twice.
    [Fact]
    public async Task Charging_the_same_order_twice_is_a_conflict()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var orderId = NextOrderId();

        Assert.Equal(
            HttpStatusCode.Created,
            (await client.PostAsJsonAsync("/payments/charge", new { orderId, amount = 10m })).StatusCode);

        var retry = await client.PostAsJsonAsync("/payments/charge", new { orderId, amount = 10m });

        Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);

        // And exactly one payment landed for that order.
        await using var db = app.NewDbContext();
        Assert.Equal(1, await db.Payments.CountAsync(p => p.OrderId == orderId));
    }

    [Theory]
    [InlineData(0, 10)]     // no order reference
    [InlineData(-1, 10)]    // negative order reference
    [InlineData(1, 0)]      // zero amount
    [InlineData(1, -5)]     // negative amount
    public async Task Charge_with_an_invalid_request_is_rejected(int orderId, decimal amount)
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        var response = await app.Client.PostAsJsonAsync("/payments/charge", new { orderId, amount });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_payment_is_not_found()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/payments/987654")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/payments/by-order/987654")).StatusCode);
    }

    private static int NextOrderId() => Interlocked.Increment(ref _nextOrderId);
}
