using System.Text.Json;
using CogniDev.Testing;
using Xunit;

namespace Orders.Tests.Contracts;

/// orders → catalog. The typed CatalogClient reads catalog's product response and
/// copies ONE scalar out of it: the price. That subset is the real contract, so it is
/// what SchemaGuard asserts — catalog is free to add, rename or drop anything orders
/// does not bind. The client's own DTO is a private nested type, so the read model is
/// restated here; if the two ever diverge, the value invariants below catch it.
public sealed class CatalogContractTests
{
    /// What Orders.Clients.CatalogClient actually binds from GET /catalog/products/{id}.
    public sealed record CatalogProductReadModel(int Id, decimal Price);

    public static IEnumerable<object[]> Fixtures() => ContractBoundaries.Fixtures("catalog");

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Provider_response_satisfies_the_read_model(string fixturePath)
    {
        var json = File.ReadAllText(fixturePath);

        SchemaGuard.AssertResponseSatisfies(json, typeof(CatalogProductReadModel));
    }

    /// Invariants the schema guard cannot know: a shape-valid response can still carry
    /// a value orders must never price an order line from.
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Provider_response_holds_the_value_invariants(string fixturePath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var root = doc.RootElement;

        var id = root.GetProperty("id").GetInt32();
        Assert.True(id > 0, "a product id must be a positive identity");

        var price = root.GetProperty("price").GetDecimal();
        Assert.True(price >= 0m, "money must never be negative");

        // Orders multiplies this by a quantity, so a null/absent price would silently
        // zero a line total rather than fail — assert it is genuinely present.
        Assert.Equal(JsonValueKind.Number, root.GetProperty("price").ValueKind);
    }
}
