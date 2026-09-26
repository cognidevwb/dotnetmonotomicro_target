using System.Net;
using System.Net.Http.Json;
using Xunit;
using CategoryResponse = Catalog.Features.Category.CategoryResponse;
using ProductPriceResponse = Catalog.Features.Product.ProductPriceResponse;
using ProductResponse = Catalog.Features.Product.ProductResponse;

namespace Catalog.Tests;

[Collection(CatalogCollection.Name)]
public sealed class ProductTests(CatalogApp app)
{
    [Fact]
    public async Task Create_then_get_round_trips_the_product()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var categoryId = await CreateCategory(client, "Hand tools");

        var created = await client.PostAsJsonAsync(
            "/catalog/products/", new { name = "Hammer", price = 19.99m, categoryId });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var body = await created.Content.ReadFromJsonAsync<ProductResponse>(CatalogJson.Web);
        Assert.NotNull(body);
        Assert.Equal("Hammer", body!.Name);
        Assert.Equal(19.99m, body.Price);
        Assert.Equal(categoryId, body.CategoryId);

        var fetched = await client.GetAsync($"/catalog/products/{body.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(body, await fetched.Content.ReadFromJsonAsync<ProductResponse>(CatalogJson.Web));
    }

    /// The cross-context read model the orders service's typed CatalogClient consumes.
    [Fact]
    public async Task Price_route_returns_the_products_price()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var categoryId = await CreateCategory(client, "Priced");

        var created = await client.PostAsJsonAsync(
            "/catalog/products/", new { name = "Chisel", price = 7.50m, categoryId });
        var product = await created.Content.ReadFromJsonAsync<ProductResponse>(CatalogJson.Web);

        var price = await client.GetFromJsonAsync<ProductPriceResponse>(
            $"/catalog/products/{product!.Id}/price", CatalogJson.Web);

        Assert.NotNull(price);
        Assert.Equal(product.Id, price!.ProductId);
        Assert.Equal(7.50m, price.Price);
    }

    [Fact]
    public async Task List_filters_by_category()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;
        var mine = await CreateCategory(client, "Mine");
        var theirs = await CreateCategory(client, "Theirs");

        await client.PostAsJsonAsync("/catalog/products/", new { name = "A", price = 1m, categoryId = mine });
        await client.PostAsJsonAsync("/catalog/products/", new { name = "B", price = 2m, categoryId = theirs });

        var filtered = await client.GetFromJsonAsync<List<ProductResponse>>(
            $"/catalog/products/?categoryId={mine}", CatalogJson.Web);

        Assert.NotNull(filtered);
        Assert.NotEmpty(filtered!);
        Assert.All(filtered!, p => Assert.Equal(mine, p.CategoryId));
    }

    [Theory]
    [InlineData("", 10, 1)]          // blank name
    [InlineData("Hammer", -1, 1)]    // negative price
    [InlineData("Hammer", 10, 0)]    // non-positive category reference
    public async Task Create_with_an_invalid_request_is_rejected(string name, decimal price, int categoryId)
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        var response = await app.Client.PostAsJsonAsync(
            "/catalog/products/", new { name, price, categoryId });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// A well-shaped request naming a category that does not exist is a state
    /// conflict, not a shape error — the handler answers 422, never a dangling row.
    [Fact]
    public async Task Create_under_an_unknown_category_is_unprocessable()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        var response = await app.Client.PostAsJsonAsync(
            "/catalog/products/", new { name = "Orphan", price = 5m, categoryId = 987654 });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_product_is_not_found()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/catalog/products/987654")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/catalog/products/987654/price")).StatusCode);
    }

    private static async Task<int> CreateCategory(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/catalog/categories/", new { name });
        var category = await response.Content.ReadFromJsonAsync<CategoryResponse>(CatalogJson.Web);
        return category!.Id;
    }
}
