using System.Text.Json;
using Korkut.Core;
using Korkut.LawClients.Services;
using Korkut.LawClients.Services.Shared;
using Korkut.LawClients.Services.Sources;
using Microsoft.Extensions.Caching.Memory;

namespace Korkut.LawClients.Unit.Tests;

/// <summary>
/// The drawer's full-text endpoint: dispatches to the matching source client, pages far wider than
/// the model tool, and turns every failure mode (unknown source, fetch-less source, missing key,
/// empty result, upstream error) into a NotFound the API maps to 404.
/// </summary>
public class LawClientsServiceTests
{
    private sealed class FakeClient(string source, string? text, bool supportsFetch = true, string? requiresKey = null) : ISourceClient
    {
        public JsonElement? LastArgs { get; private set; }

        public string Source => source;

        public string? RequiresKey => requiresKey;

        public bool SupportsFetch => supportsFetch;

        public Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SourceResult>>([]);

        public Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default)
        {
            LastArgs = args;
            return Task.FromResult(text);
        }
    }

    private sealed class FakeEnv : IEnvironment
    {
        public string? Get(string key) => null;
    }

    private static LawClientsService Service(params ISourceClient[] clients) =>
        new(clients, new FakeEnv(), new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public async Task Returns_the_document_text_with_a_wide_page_size()
    {
        var client = new FakeClient("yargitay", "# Karar metni");
        var svc = Service(client);

        var doc = await svc.GetAsync("YARGITAY", " id-1 ");

        Assert.Equal(("yargitay", "id-1", "# Karar metni"), (doc.Source, doc.Id, doc.ContentMd));
        Assert.Equal(20_000, client.LastArgs!.Value.GetProperty("page_size").GetInt32());
        Assert.Equal(1, client.LastArgs!.Value.GetProperty("page").GetInt32());
    }

    [Fact]
    public async Task Unknown_source_fetchless_source_and_missing_key_all_map_to_not_found()
    {
        var svc = Service(
            new FakeClient("nofetch", "x", supportsFetch: false), new FakeClient("keyed", "x", requiresKey: "MISSING_KEY"));

        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync("bilinmeyen", "id"));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync("nofetch", "id"));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync("keyed", "id"));
    }

    [Fact]
    public async Task An_empty_result_maps_to_not_found()
    {
        var svc = Service(new FakeClient("yargitay", "  "));
        await Assert.ThrowsAsync<NotFoundException>(() => svc.GetAsync("yargitay", "id"));
    }
}
