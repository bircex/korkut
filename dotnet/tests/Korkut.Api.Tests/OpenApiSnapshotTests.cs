using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Korkut.Api.Tests;

/// <summary>
/// Snapshots the generated OpenAPI document and fails on drift, so an API-surface
/// change cannot merge without an intentional, reviewed snapshot update — the
/// breaking-change guard required by KDM-006. Regenerate deliberately with
/// KORKUT_OPENAPI_UPDATE=1.
/// </summary>
public class OpenApiSnapshotTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task OpenApi_document_matches_snapshot()
    {
        var live = Normalize(await factory.CreateClient().GetStringAsync("/openapi/v1.json"));
        var path = SnapshotPath();

        if (Environment.GetEnvironmentVariable("KORKUT_OPENAPI_UPDATE") == "1")
        {
            await File.WriteAllTextAsync(path, live + "\n");
            return;
        }

        Assert.True(File.Exists(path), $"OpenAPI snapshot missing at {path}; regenerate with KORKUT_OPENAPI_UPDATE=1.");
        var committed = Normalize(await File.ReadAllTextAsync(path));
        Assert.True(
            live == committed,
            "OpenAPI contract drifted. If intended, regenerate with KORKUT_OPENAPI_UPDATE=1 and review the diff.");
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    // Reserialize with sorted keys so formatting/ordering noise never causes a false diff.
    private static string Normalize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(Sort(doc.RootElement), Indented);
    }

    private static object? Sort(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(p => p.Name, p => Sort(p.Value)),
        JsonValueKind.Array => element.EnumerateArray().Select(Sort).ToList(),
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null,
    };

    private static string SnapshotPath([CallerFilePath] string thisFile = "")
        => Path.Combine(Path.GetDirectoryName(thisFile)!, "Snapshots", "openapi.v1.json");
}
