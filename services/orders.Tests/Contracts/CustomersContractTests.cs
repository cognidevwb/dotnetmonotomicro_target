using System.Text.Json;
using CogniDev.Testing;
using Xunit;

namespace Orders.Tests.Contracts;

/// orders → customers. The typed CustomersClient asks customers exactly one question
/// before accepting an order: is this customer active? It binds `id` and `active` and
/// nothing else, so that subset is the contract SchemaGuard enforces.
///
/// NOTE: the client's private DTO also declares a `Name` member that the customers
/// service never sends (it serves `email`). It is unread and nullable, so it cannot
/// fail at runtime — but it is NOT part of the contract and is deliberately excluded
/// from the read model below rather than asserted into existence.
public sealed class CustomersContractTests
{
    /// What Orders.Clients.CustomersClient actually binds from GET /customers/{id}.
    public sealed record CustomerReadModel(int Id, bool Active);

    public static IEnumerable<object[]> Fixtures() => ContractBoundaries.Fixtures("customers");

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Provider_response_satisfies_the_read_model(string fixturePath)
    {
        var json = File.ReadAllText(fixturePath);

        SchemaGuard.AssertResponseSatisfies(json, typeof(CustomerReadModel));
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Provider_response_holds_the_value_invariants(string fixturePath)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var root = doc.RootElement;

        Assert.True(root.GetProperty("id").GetInt32() > 0, "a customer id must be a positive identity");

        // Activation is a real boolean, not a stringly-typed legacy status: orders
        // branches on it directly, so a string here would bind to `false` silently.
        var active = root.GetProperty("active");
        Assert.True(
            active.ValueKind is JsonValueKind.True or JsonValueKind.False,
            $"'active' must be a JSON boolean, was {active.ValueKind}");
    }
}
