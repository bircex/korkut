using System.Runtime.CompilerServices;
using Korkut.Application.Ai;
using Korkut.Application.Runtime;
using Korkut.Infrastructure.Ai;

namespace Korkut.Infrastructure.Tests;

/// <summary>
/// Unit coverage for the model abstraction: the router resolves specialty → provider +
/// model (honouring the ported ModelRouting + the local-fallback rule), and the routing
/// facade dispatches to the matching provider and returns its reply/usage. No network,
/// no DB — the whole point is that the seam is testable in isolation.
/// </summary>
public class AiRoutingTests
{
    private sealed class FakeEnv(Dictionary<string, string?> vars) : IEnvironment
    {
        public string? Get(string name) => vars.TryGetValue(name, out var v) ? v : null;
    }

    private sealed class FakeProbe(bool available) : ILocalModelProbe
    {
        public bool IsAvailable => available;
    }

    private sealed class FakeProvider(string name, string reply) : IChatProvider
    {
        public ModelSelection? LastSelection { get; private set; }

        public string Name => name;

        public Task<ChatReply> CompleteAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default)
        {
            LastSelection = selection;
            return Task.FromResult(new ChatReply(reply, new ChatUsage(name, selection.ModelId, 1, 2)));
        }

        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
            ModelSelection selection, ChatRequest request, [EnumeratorCancellation] CancellationToken ct = default)
        {
            LastSelection = selection;
            yield return ChatStreamEvent.Text(reply);
            yield return ChatStreamEvent.Final(new ChatUsage(name, selection.ModelId, 1, 2));
            await Task.CompletedTask;
        }
    }

    private static ModelRouting Routing() => new(
        new Dictionary<string, string> { ["cloud"] = "claude", ["local"] = "ollama" },
        new Dictionary<string, string> { ["petition"] = "cloud", ["hearing"] = "local" },
        fallbackToCloudIfNoLocal: true);

    private static ModelRouter Router(bool localAvailable = false) => new(
        Routing(),
        new RuntimeConfig(new FakeEnv(new() { ["ANTHROPIC_MODEL"] = "claude-opus-4-8" })),
        new FakeEnv(new()),
        new FakeProbe(localAvailable));

    [Fact]
    public void Router_maps_a_specialty_to_its_configured_provider_and_model()
    {
        var sel = Router().Resolve("petition", new HashSet<string> { "claude" });
        Assert.Equal("claude", sel.Provider);
        Assert.Equal("claude-opus-4-8", sel.ModelId);
    }

    [Fact]
    public void A_local_specialty_falls_back_to_cloud_when_no_local_backend()
    {
        var sel = Router(localAvailable: false).Resolve("hearing", new HashSet<string> { "claude" });
        Assert.Equal("claude", sel.Provider); // hearing→local, but local down + fallback → cloud → claude
    }

    [Fact]
    public void A_tier_whose_provider_is_not_registered_throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => Router().Resolve("petition", new HashSet<string> { "groq" })); // cloud=claude, not available
    }

    [Fact]
    public async Task Facade_dispatches_to_the_provider_the_router_picks()
    {
        var claude = new FakeProvider("claude", "CLAUDE-REPLY");
        var groq = new FakeProvider("groq", "GROQ-REPLY");
        var model = new RoutingChatModel(Router(), new IChatProvider[] { claude, groq });

        var reply = await model.CompleteAsync(new ChatRequest("sys", [new ChatMessage("user", "selam")], ModelSpecialty.Petition));

        Assert.Equal("CLAUDE-REPLY", reply.Text);
        Assert.Equal("claude", reply.Usage.Provider);
        Assert.Equal("claude-opus-4-8", claude.LastSelection!.ModelId); // resolved model flowed to the provider
    }

    [Fact]
    public async Task Facade_surfaces_a_routing_failure()
    {
        var badRouter = new ModelRouter(
            new ModelRouting(new Dictionary<string, string> { ["cloud"] = "gpt", ["local"] = "ollama" }, new Dictionary<string, string>(), true),
            new RuntimeConfig(new FakeEnv(new())),
            new FakeEnv(new()),
            new FakeProbe(false));
        var model = new RoutingChatModel(badRouter, new IChatProvider[] { new FakeProvider("claude", "x") });

        // cloud=gpt but only claude is registered → the router refuses to resolve.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => model.CompleteAsync(new ChatRequest("s", [new ChatMessage("user", "x")], ModelSpecialty.Petition)));
    }

    [Fact]
    public async Task Streaming_yields_text_deltas_then_a_terminal_usage()
    {
        var model = new RoutingChatModel(Router(), new IChatProvider[] { new FakeProvider("claude", "MERHABA") });

        var deltas = new List<string>();
        ChatUsage? final = null;
        await foreach (var ev in model.StreamAsync(new ChatRequest("s", [new ChatMessage("user", "x")], ModelSpecialty.Petition)))
        {
            if (ev.Delta is { } d)
            {
                deltas.Add(d);
            }

            if (ev.Usage is { } u)
            {
                final = u;
            }
        }

        Assert.Equal("MERHABA", string.Concat(deltas));
        Assert.NotNull(final);
        Assert.Equal("claude", final!.Provider);
    }
}
