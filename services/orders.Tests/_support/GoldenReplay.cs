using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Testcontainers.PostgreSql;

namespace Orders.Tests;

/// A recorded legacy request → response golden.
public sealed record GoldenCase(GoldenRequest Request, JsonElement? Seed, JsonElement ExpectedResponse);
public sealed record GoldenRequest(string Method, string Path, JsonElement? Body);

/// Golden-master harness for Orders. Usage in a test:
///   await using var replay = new GoldenReplay();
///   await replay.StartAsync();
///   var (status, json) = await replay.Replay(goldenCase);
public sealed class GoldenReplay : IAsyncDisposable
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder().Build();
    private WebApplicationFactory<Program>? _factory;

    public async Task StartAsync()
    {
        await _db.StartAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.UseSetting("ConnectionStrings:ordersdb", _db.GetConnectionString()));
    }

    public HttpClient Client => _factory!.CreateClient();

    public async Task<(int Status, string Json)> Replay(GoldenCase c)
    {
        var req = new HttpRequestMessage(new HttpMethod(c.Request.Method), c.Request.Path);
        if (c.Request.Body is { } body)
            req.Content = new StringContent(body.GetRawText(), Encoding.UTF8, "application/json");
        var resp = await Client.SendAsync(req);
        var json = await resp.Content.ReadAsStringAsync();
        return ((int)resp.StatusCode, json);
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null) await _factory.DisposeAsync();
        await _db.DisposeAsync();
    }
}
