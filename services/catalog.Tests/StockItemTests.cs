using System.Net;
using System.Net.Http.Json;
using Catalog.Domain;
using Xunit;
using StockItemEntity = Catalog.Domain.StockItem;
using StockItemResponse = Catalog.Features.StockItem.StockItemResponse;

namespace Catalog.Tests;

[Collection(CatalogCollection.Name)]
public sealed class StockItemTests(CatalogApp app)
{
    // Each test owns a distinct product id so the concurrency test cannot be
    // perturbed by a sibling reserving the same row.
    private static int _nextProductId = 900_000;

    [Fact]
    public async Task Get_returns_the_seeded_stock_item()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var productId = await Seed(quantity: 12, StockStatus.InStock);

        var response = await app.Client.GetAsync($"/catalog/stock/{productId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StockItemResponse>(CatalogJson.Web);
        Assert.NotNull(body);
        Assert.Equal(productId, body!.ProductId);
        Assert.Equal(12, body.Quantity);
        Assert.Equal(StockStatus.InStock, body.Status);
    }

    [Fact]
    public async Task Reserve_decrements_the_quantity()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var productId = await Seed(quantity: 10, StockStatus.InStock);

        var response = await app.Client.PostAsJsonAsync(
            "/catalog/stock/reservations", new { productId, quantity = 4 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<StockItemResponse>(CatalogJson.Web);
        Assert.Equal(6, body!.Quantity);
        Assert.Equal(StockStatus.InStock, body.Status);
    }

    /// Draining the row to exactly zero must flip the status, not just the count.
    [Fact]
    public async Task Reserving_the_last_unit_marks_the_item_out_of_stock()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var productId = await Seed(quantity: 3, StockStatus.InStock);

        var response = await app.Client.PostAsJsonAsync(
            "/catalog/stock/reservations", new { productId, quantity = 3 });

        var body = await response.Content.ReadFromJsonAsync<StockItemResponse>(CatalogJson.Web);
        Assert.Equal(0, body!.Quantity);
        Assert.Equal(StockStatus.OutOfStock, body.Status);
    }

    [Fact]
    public async Task Reserving_more_than_is_held_is_a_conflict()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var productId = await Seed(quantity: 2, StockStatus.InStock);

        var response = await app.Client.PostAsJsonAsync(
            "/catalog/stock/reservations", new { productId, quantity = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        // And the guard left the row untouched — no partial decrement.
        var after = await app.Client.GetFromJsonAsync<StockItemResponse>(
            $"/catalog/stock/{productId}", CatalogJson.Web);
        Assert.Equal(2, after!.Quantity);
    }

    [Fact]
    public async Task Reserving_a_discontinued_item_is_a_conflict()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var productId = await Seed(quantity: 50, StockStatus.Discontinued);

        var response = await app.Client.PostAsJsonAsync(
            "/catalog/stock/reservations", new { productId, quantity = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// The reason the handler uses one guarded UPDATE instead of read-check-then-write:
    /// two concurrent reservations that TOGETHER exceed the stock must not both win.
    [Fact]
    public async Task Concurrent_reservations_cannot_oversell()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var productId = await Seed(quantity: 10, StockStatus.InStock);

        var first = app.Client.PostAsJsonAsync("/catalog/stock/reservations", new { productId, quantity = 6 });
        var second = app.Client.PostAsJsonAsync("/catalog/stock/reservations", new { productId, quantity = 6 });
        var responses = await Task.WhenAll(first, second);

        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var after = await app.Client.GetFromJsonAsync<StockItemResponse>(
            $"/catalog/stock/{productId}", CatalogJson.Web);
        Assert.Equal(4, after!.Quantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Reserving_a_non_positive_quantity_is_rejected(int quantity)
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var productId = await Seed(quantity: 5, StockStatus.InStock);

        var response = await app.Client.PostAsJsonAsync(
            "/catalog/stock/reservations", new { productId, quantity });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Stock_for_an_unknown_product_is_not_found()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/catalog/stock/987654")).StatusCode);

        var reserve = await app.Client.PostAsJsonAsync(
            "/catalog/stock/reservations", new { productId = 987654, quantity = 1 });
        Assert.Equal(HttpStatusCode.NotFound, reserve.StatusCode);
    }

    private async Task<int> Seed(int quantity, StockStatus status)
    {
        var productId = Interlocked.Increment(ref _nextProductId);
        await using var db = app.NewDbContext();
        db.StockItems.Add(new StockItemEntity { ProductId = productId, Quantity = quantity, Status = status });
        await db.SaveChangesAsync();
        return productId;
    }
}
