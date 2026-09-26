using NetArchTest.Rules;
using Xunit;

namespace Customers.Tests;

// Enforced clean architecture: the Domain must stay pure. If this fails, a Domain type has
// reached for Infrastructure / EF / the bus — move the dependency behind the slice handler.
public sealed class ArchitectureTests
{
    static readonly System.Reflection.Assembly Service = typeof(Program).Assembly;

    [Fact]
    public void Domain_has_no_dependency_on_Infrastructure_or_messaging()
    {
        var result = Types.InAssembly(Service)
            .That().ResideInNamespace("Customers.Domain")
            .ShouldNot().HaveDependencyOnAny(
                "Customers.Infrastructure",
                "Microsoft.EntityFrameworkCore",
                "Wolverine",
                "MassTransit",
                "Npgsql",
                "Dapper")
            .GetResult();

        // Only touch FailingTypes on failure (it is null on success) — string.Join over the
        // collection is robust to the element type via ToString().
        if (!result.IsSuccessful)
        {
            Assert.Fail(
                "Domain must not depend on infrastructure. Offending type(s): "
                    + string.Join(", ", result.FailingTypes));
        }
    }
}
