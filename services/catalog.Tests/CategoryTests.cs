using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Catalog.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;
using CategoryResponse = Catalog.Features.Category.CategoryResponse;

namespace Catalog.Tests;

/// Hosts the real Catalog app against a real PostgreSQL (Testcontainers) so the slice
/// tests exercise the guarded stock UPDATE and the EF mappings for real, rather than
/// against an in-memory provider that cannot reproduce the concurrency semantics.
/// If no container runtime is available the start fails and every test SKIPS, so the
/// suite stays green on machines without Docker.
public sealed class CatalogApp : IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().Build();
    private WebApplicationFactory<Program>? _factory;

    /// Non-null when the container could not start; tests skip on it.
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

        // Create the schema through a standalone context BEFORE the app boots: the
        // Wolverine message store provisions its own tables at startup, and
        // EnsureCreated is a no-op once the database contains any table.
        await using (var seed = NewDbContext())
        {
            await seed.Database.EnsureCreatedAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:catalogdb", _db.GetConnectionString()));
    }

    public HttpClient Client => _factory!.CreateClient();

    public CatalogDbContext NewDbContext() => new(
        new DbContextOptionsBuilder<CatalogDbContext>()
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

[CollectionDefinition(CatalogCollection.Name)]
public sealed class CatalogCollection : ICollectionFixture<CatalogApp>
{
    public const string Name = "catalog-app";
}

/// The endpoints serialize with the ASP.NET web defaults (camelCase, enums as numbers).
internal static class CatalogJson
{
    public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
}

[Collection(CatalogCollection.Name)]
public sealed class CategoryTests(CatalogApp app)
{
    [Fact]
    public async Task Create_then_get_round_trips_the_category()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;

        var created = await client.PostAsJsonAsync("/catalog/categories/", new { name = "Tools" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var body = await created.Content.ReadFromJsonAsync<CategoryResponse>(CatalogJson.Web);
        Assert.NotNull(body);
        Assert.Equal("Tools", body!.Name);
        Assert.True(body.Id > 0);

        var fetched = await client.GetAsync($"/catalog/categories/{body.Id}");
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);
        Assert.Equal(body, await fetched.Content.ReadFromJsonAsync<CategoryResponse>(CatalogJson.Web));
    }

    [Fact]
    public async Task List_includes_a_created_category()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");
        var client = app.Client;

        var created = await client.PostAsJsonAsync("/catalog/categories/", new { name = "Listed" });
        var body = await created.Content.ReadFromJsonAsync<CategoryResponse>(CatalogJson.Web);

        var all = await client.GetFromJsonAsync<List<CategoryResponse>>("/catalog/categories/", CatalogJson.Web);

        Assert.NotNull(all);
        Assert.Contains(all!, c => c.Id == body!.Id);
    }

    [Fact]
    public async Task Create_with_blank_name_is_rejected()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        var response = await app.Client.PostAsJsonAsync("/catalog/categories/", new { name = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Name", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_with_overlong_name_is_rejected()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        var response = await app.Client.PostAsJsonAsync(
            "/catalog/categories/", new { name = new string('x', 201) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_category_is_not_found()
    {
        Assert.SkipWhen(app.Unavailable is not null, app.Unavailable ?? "");

        var response = await app.Client.GetAsync("/catalog/categories/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
