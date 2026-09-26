using System.Reflection;
using System.Text.Json;
using Xunit.Sdk;

namespace CogniDev.Testing;

/// Assert a provider JSON response is a SUPERSET of what a consumer read-model needs:
/// every readable member of the contract type must be present (camelCase or PascalCase)
/// with a compatible JSON kind. Catches a provider dropping/renaming a field a consumer
/// binds — without a broker or PactNet (in-box System.Text.Json only).
public static class SchemaGuard
{
    public static void AssertResponseSatisfies(string json, Type contract)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new XunitException($"expected a JSON object, got {root.ValueKind}");

        foreach (var p in contract.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!p.CanRead) continue;
            if (!TryGet(root, p.Name, out var value))
                throw new XunitException(
                    $"contract {contract.Name} requires member '{p.Name}', but the provider response has no such property");
            if (!KindCompatible(value.ValueKind, p.PropertyType))
                throw new XunitException(
                    $"member '{p.Name}': provider JSON kind {value.ValueKind} is not compatible with {p.PropertyType.Name}");
        }
    }

    private static bool TryGet(JsonElement obj, string name, out JsonElement value)
    {
        if (obj.TryGetProperty(name, out value)) return true;
        var camel = char.ToLowerInvariant(name[0]) + name.Substring(1);
        return obj.TryGetProperty(camel, out value);
    }

    private static bool KindCompatible(JsonValueKind kind, Type declared)
    {
        var t = Nullable.GetUnderlyingType(declared) ?? declared;
        if (kind == JsonValueKind.Null)
            return !t.IsValueType || Nullable.GetUnderlyingType(declared) is not null;
        if (t == typeof(bool)) return kind is JsonValueKind.True or JsonValueKind.False;
        if (t == typeof(string) || t.IsEnum || t == typeof(Guid) || t == typeof(DateTime) || t == typeof(DateTimeOffset))
            return kind is JsonValueKind.String or JsonValueKind.Number;
        if (t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(decimal) || t == typeof(double) || t == typeof(float))
            return kind == JsonValueKind.Number;
        // objects / collections / anything else: accept any non-absent value.
        return true;
    }
}
