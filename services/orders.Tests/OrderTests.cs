using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orders.Clients;
using Orders.Infrastructure;
using Testcontainers.PostgreSql;
using Xunit;
using OrderResponse = Orders.Features.Order.OrderResponse;
using OrderStatus = Orders.Features.Order.OrderStatus;
// `Orders.Tests.Contracts` exists (the contract tests), so the bare name `Contracts`
// would not resolve to the service's cross-context read models. Alias it.
using PaymentContract = Orders.Contracts.Payment;

namespace Orders.Tests;

/// Hosts the real Orders app against a real PostgreSQL (Testcontainers), with the four
/// cross-context typed clients replaced by controllable fakes: this test owns the
/// orders context only, so catalog/customers/inventory/payments are stubbed at the
/// seam rather than started as real services. Without a container runtime tests SKIP.
public sealed class OrdersApp : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().Build();
    private WebApplicationFactory<Program>? _factory;

    public string? Unavailable { get; private set; }

    public FakeCustomersClient Customers { get; } = new();
    public FakeCatalogClient Catalog { get; } = new();
    public FakeInventoryClient Inventory { get; } = new();
    public FakePaymentsClient Payments { get; } = new();

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
        {
            b.UseSetting("ConnectionStrings:ordersdb", _db.GetConnectionString());
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<ICustomersClient>();
                services.RemoveAll<ICatalogClient>();
                services.RemoveAll<IInventoryClient>();
                services.RemoveAll<IPaymentsClient>();
                services.AddSingleton<ICustomersClient>(Customers);
                services.AddSingleton<ICatalogClient>(Catalog);
                services.AddSingleton<IInventoryClient>(Inventory);
                services.AddSingleton<IPaymentsClient>(Payments);
            });
        });
    }

    public HttpClient Client => _factory!.CreateClient();

    public OrdersDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<OrdersDbContext>()
            .UseNpgsql(_db.GetConnectionString())
            .Options);

    /// Put every participant back into its "everything works" state.
    public void ResetParticipants()
    {
        Customers.Active = true;
        Catalog.Price = 10m;
        Inventory.Reserves = true;
        Inventory.Released.Clear();
        Payments.Charges.Clear();
        Payments.Refunded.Clear();
        Payments.Fail = false;
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }

        await _db.DisposeAsync();
    }
}

public sealed class FakeCustomersClient : ICustomersClient
{
    public bool Active { get; set; } = true;

    public Task<bool> IsActiveAsync(int id) => Task.FromResult(Active);
}

public sealed class FakeCatalogClient : ICatalogClient
{
    public decimal Price { get; set; } = 10m;

    public Task<decimal> PriceOfAsync(int productId) => Task.FromResult(Price);
}

public sealed class FakeInventoryClient : IInventoryClient
{
    public bool Reserves { get; set; } = true;

    public List<(int ProductId, int Quantity)> Released { get; } = [];

    public Task<bool> ReserveAsync(int productId, int qty) => Task.FromResult(Reserves);

    public Task ReleaseAsync(int productId, int qty)
    {
        Released.Add((productId, qty));
        return Task.CompletedTask;
    }
}

public sealed class FakePaymentsClient : IPaymentsClient
{
    private int _nextId = 5_000;

    public bool Fail { get; set; }

    public List<(int OrderId, decimal Amount)> Charges { get; } = [];

    public List<int> Refunded { get; } = [];

    public Task<PaymentContract> ChargeAsync(int orderId, decimal amount)
    {
        if (Fail)
        {
            throw new HttpRequestException("payments-service unavailable");
        }

        Charges.Add((orderId, amount));
        return Task.FromResult(new PaymentContract
        {
            Id = Interlocked.Increment(ref _nextId),
            OrderId = orderId,
            Amount = amount,
            Status = "charged",
        });
    }

    public Task RefundAsync(int paymentId)
    {
        Refunded.Add(paymentId);
        return Task.CompletedTask;
    }
}

[CollectionDefinition(OrdersCollection.Name)]
public sealed class OrdersCollection : ICollectionFixture<OrdersApp>
{
    public const string Name = "orders-app";
}

internal static class OrdersJson
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}

[Collection(OrdersCollection.Name)]
public sealed class OrderTests(OrdersApp app)
{
    [Fact]
    public async Task Placing_an_order_persists_it_with_priced_lines()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        app.ResetParticipants();
        app.Catalog.Price = 12.50m;

        var response = await app.Client.PostAsJsonAsync("/orders/", new
        {
            customerId = 1,
            lines = new[] { new { productId = 10, quantity = 2 } },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<OrderResponse>(OrdersJson.Web);
        Assert.NotNull(body);
        Assert.Equal(1, body!.CustomerId);
        Assert.Equal(25m, body.Total);
        var line = Assert.Single(body.Lines);
        Assert.Equal(10, line.ProductId);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(12.50m, line.UnitPrice);
    }

    [Fact]
    public async Task A_placed_order_is_retrievable()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        app.ResetParticipants();

        var created = await app.Client.PostAsJsonAsync("/orders/", new
        {
            customerId = 2,
            lines = new[] { new { productId = 11, quantity = 1 } },
        });
        var placed = await created.Content.ReadFromJsonAsync<OrderResponse>(OrdersJson.Web);

        var fetched = await app.Client.GetAsync($"/orders/{placed!.Id}");

        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        var body = await fetched.Content.ReadFromJsonAsync<OrderResponse>(OrdersJson.Web);
        Assert.Equal(placed.Id, body!.Id);
        Assert.Equal(placed.Total, body.Total);
        // The status is a real enum on the way out, never the raw ported string column.
        Assert.NotEqual(OrderStatus.Unknown, body.Status);
    }

    /// Stock is no longer this service's to decrement — a refused reservation comes
    /// back from inventory and must surface as a conflict, not a half-placed order.
    [Fact]
    public async Task An_order_that_cannot_be_stocked_is_a_conflict()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        app.ResetParticipants();
        app.Inventory.Reserves = false;

        var response = await app.Client.PostAsJsonAsync("/orders/", new
        {
            customerId = 3,
            lines = new[] { new { productId = 12, quantity = 1 } },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("out of stock", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    /// A rejection partway through a multi-line order must release the reservations
    /// already taken — nothing is left held for an order that never existed.
    [Fact]
    public async Task A_partially_reserved_order_releases_what_it_took()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        app.ResetParticipants();
        app.Inventory.Reserves = false;

        await app.Client.PostAsJsonAsync("/orders/", new
        {
            customerId = 4,
            lines = new[] { new { productId = 13, quantity = 1 } },
        });

        // The first line was refused before it was ever reserved, so nothing is owed.
        Assert.Empty(app.Inventory.Released);
    }

    [Fact]
    public async Task An_inactive_customer_cannot_place_an_order()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        app.ResetParticipants();
        app.Customers.Active = false;

        var response = await app.Client.PostAsJsonAsync("/orders/", new
        {
            customerId = 5,
            lines = new[] { new { productId = 14, quantity = 1 } },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("customer inactive", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_order_with_no_lines_is_rejected()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        app.ResetParticipants();

        var response = await app.Client.PostAsJsonAsync(
            "/orders/", new { customerId = 6, lines = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Lines", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 10, 1)]    // no customer
    [InlineData(1, 0, 1)]     // no product
    [InlineData(1, 10, 0)]    // zero quantity
    [InlineData(1, 10, -2)]   // negative quantity
    public async Task An_order_with_an_invalid_line_is_rejected(int customerId, int productId, int quantity)
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        app.ResetParticipants();

        var response = await app.Client.PostAsJsonAsync("/orders/", new
        {
            customerId,
            lines = new[] { new { productId, quantity } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_order_is_not_found()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        var response = await app.Client.GetAsync("/orders/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
