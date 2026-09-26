using System.Text.Json;
using CogniDev.Testing;
using Xunit;

namespace Catalog.Tests.Equivalence;

/// Golden-master equivalence for the catalog aggregates. Each fixture under
/// `Equivalence/fixtures/&lt;aggregate&gt;/` is a `{request, seed, expectedResponse}` golden
/// derived from the PORTED legacy action; the test replays it against the real service
/// (GoldenReplay) and compares the canonicalized JSON.
///
/// Comparison is SUPERSET-shaped: only the members recorded in `expectedResponse` are
/// compared, so a framework-added extension (`traceId`, the ProblemDetails `type` URI)
/// cannot make a golden fail for a reason that has nothing to do with behaviour.
public sealed class CatalogEquivalenceTests : IAsyncLifetime
{
    private GoldenReplay? _replay;
    private string? _unavailable;

    public async ValueTask InitializeAsync()
    {
        _replay = new GoldenReplay();
        try
        {
            await _replay.StartAsync();
        }
        catch (Exception ex)
        {
            _unavailable = $"no container runtime available: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_replay is not null)
        {
            await _replay.DisposeAsync();
        }
    }

    public static IEnumerable<object[]> Goldens() => GoldenFixtures.All();

    [Theory]
    [MemberData(nameof(Goldens))]
    public async Task New_service_matches_the_legacy_golden(string path)
    {
        Assert.SkipWhen(_unavailable is not null, _unavailable ?? "");

        var golden = GoldenFixtures.Load(path);
        var (status, json) = await _replay!.Replay(golden);

        Assert.Equal(GoldenFixtures.ExpectedStatus(golden), status);

        var expected = golden.ExpectedResponse.GetRawText();
        Assert.Equal(
            JsonNormalize.Canonical(expected, GoldenFixtures.Volatile),
            JsonNormalize.Canonical(GoldenFixtures.ProjectOnto(json, expected), GoldenFixtures.Volatile));
    }
}

/// Shared loading/normalizing helpers for the golden fixtures in this project.
internal static class GoldenFixtures
{
    /// Generated values that must not decide equivalence.
    public static readonly string[] Volatile = ["id", "createdAt", "updatedAt", "timestamp", "correlationId"];

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static IEnumerable<object[]> All()
    {
        var root = Path.Combine(AppContext.BaseDirectory, "Equivalence", "fixtures");
        return Directory.Exists(root)
            ? Directory.EnumerateFiles(root, "*.json", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.Ordinal)
                .Select(f => new object[] { f })
            : [];
    }

    public static GoldenCase Load(string path) =>
        JsonSerializer.Deserialize<GoldenCase>(File.ReadAllText(path), Web)
        ?? throw new InvalidOperationException($"golden fixture '{path}' is empty");

    /// The recorded status lives on `expectedResponse.status` for ProblemDetails
    /// goldens; success goldens carry no status member and are expected to be 200.
    public static int ExpectedStatus(GoldenCase golden) =>
        golden.ExpectedResponse.ValueKind == JsonValueKind.Object
        && golden.ExpectedResponse.TryGetProperty("status", out var status)
        && status.ValueKind == JsonValueKind.Number
            ? status.GetInt32()
            : 200;

    /// Keep only the members the golden actually recorded, so unrecorded framework
    /// extensions are ignored rather than silently asserted.
    public static string ProjectOnto(string actualJson, string expectedJson)
    {
        using var actual = JsonDocument.Parse(actualJson);
        using var expected = JsonDocument.Parse(expectedJson);

        if (actual.RootElement.ValueKind != JsonValueKind.Object ||
            expected.RootElement.ValueKind != JsonValueKind.Object)
        {
            return actualJson;
        }

        var projected = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var member in expected.RootElement.EnumerateObject())
        {
            if (actual.RootElement.TryGetProperty(member.Name, out var value))
            {
                projected[member.Name] = value.Clone();
            }
        }

        return JsonSerializer.Serialize(projected, Web);
    }
}
