namespace Orders.Tests;

/// The sync boundaries orders depends on. Each entry has a Contracts/fixtures/<provider>/
/// directory of recorded provider responses the contract tests assert against.
public static class ContractBoundaries
{
    public static readonly (string Consumer, string Provider)[] All =
    {
        ("orders", "catalog"),
        ("orders", "customers"),
        ("orders", "payments"),
    };

    /// Feeds [MemberData]: every recorded fixture file for a given provider.
    public static IEnumerable<object[]> Fixtures(string provider)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Contracts", "fixtures", provider);
        return Directory.Exists(dir)
            ? Directory.EnumerateFiles(dir, "*.json").Select(f => new object[] { f })
            : Enumerable.Empty<object[]>();
    }
}
