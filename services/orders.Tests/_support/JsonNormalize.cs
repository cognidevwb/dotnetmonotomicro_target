using System.Text.Json;
using System.Text.Json.Nodes;

namespace CogniDev.Testing;

/// Canonicalize JSON for equivalence asserts: sort object keys and mask volatile fields
/// (ids/timestamps/correlation ids) so the ported service and the legacy monolith
/// compare equal on business content, not on generated values.
public static class JsonNormalize
{
    private static readonly string[] DefaultVolatile =
        { "id", "createdAt", "updatedAt", "timestamp", "correlationId" };

    public static string Canonical(string json, params string[] volatileMembers)
    {
        var masked = volatileMembers.Length == 0 ? DefaultVolatile : volatileMembers;
        var node = JsonNode.Parse(json);
        return Rewrite(node, masked)?.ToJsonString() ?? "null";
    }

    private static JsonNode? Rewrite(JsonNode? node, string[] masked)
    {
        switch (node)
        {
            case JsonObject obj:
                var result = new JsonObject();
                foreach (var key in obj.Select(kv => kv.Key).OrderBy(k => k, StringComparer.Ordinal))
                {
                    if (masked.Contains(key, StringComparer.OrdinalIgnoreCase))
                        result[key] = "<masked>";
                    else
                        result[key] = Rewrite(obj[key]?.DeepClone(), masked);
                }
                return result;
            case JsonArray arr:
                var outArr = new JsonArray();
                foreach (var item in arr)
                    outArr.Add(Rewrite(item?.DeepClone(), masked));
                return outArr;
            default:
                return node?.DeepClone();
        }
    }
}
