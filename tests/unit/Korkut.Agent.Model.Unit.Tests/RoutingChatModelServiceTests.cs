using Korkut.Agent.Model.Services;
using Korkut.Contract.Agent.Model;
using Korkut.Core;
using ICurrentUser = Korkut.Contract.Agent.ICurrentUser;

namespace Korkut.Agent.Model.Unit.Tests;

/// <summary>
/// The full precedence chain <see cref="RoutingChatModelService"/> applies — office pin, then the
/// local backend when a specialty wants one and it's reachable, then the dynamic system default,
/// then a loud failure. This is the one place those three pieces (<see cref="IOfficeModelOverrideService"/>'s
/// two halves + <see cref="IModelRouterService"/>) are merged, so it is the seam that proves NOTHING
/// in the merged result ever falls back to a hardcoded vendor guess.
/// </summary>
public sealed class RoutingChatModelServiceTests
{
    private static readonly Guid OfficeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task An_office_pin_wins_regardless_of_specialty()
    {
        var sut = Build(
            office: new OfficeModelOverride("claude", "claude-opus-4-8", null, ModelSelectionSource.Office, "claude"),
            local: new ModelSelection("ollama", "llama3.1", 4096), // even though hearing wants + has local
            systemDefault: new OfficeModelOverride("deepseek", "deepseek-chat", null, ModelSelectionSource.SystemDefault, "deepseek-v3"),
            providers: [new FakeProvider("claude"), new FakeProvider("ollama"), new FakeProvider("deepseek")]);

        var reply = await sut.CompleteAsync(Request("hearing"));

        Assert.Equal("claude", reply.Usage.Provider);
    }

    [Fact]
    public async Task With_no_office_pin_a_local_capable_specialty_uses_the_local_backend()
    {
        var sut = Build(
            office: null,
            local: new ModelSelection("ollama", "llama3.1", 4096),
            systemDefault: new OfficeModelOverride("deepseek", "deepseek-chat", null, ModelSelectionSource.SystemDefault, "deepseek-v3"),
            providers: [new FakeProvider("ollama"), new FakeProvider("deepseek")]);

        var reply = await sut.CompleteAsync(Request("hearing"));

        Assert.Equal("ollama", reply.Usage.Provider);
    }

    [Fact]
    public async Task With_no_office_pin_and_no_local_backend_the_system_default_is_used()
    {
        var sut = Build(
            office: null,
            local: null, // dispatcher never wants local, or hearing wants it but none is reachable
            systemDefault: new OfficeModelOverride("gemini", "gemini-3.5-flash", null, ModelSelectionSource.SystemDefault, "gemini"),
            providers: [new FakeProvider("gemini")]);

        var reply = await sut.CompleteAsync(Request("dispatcher"));

        Assert.Equal("gemini", reply.Usage.Provider);
        Assert.Equal("gemini-3.5-flash", reply.Usage.ModelId);
    }

    [Fact]
    public async Task With_nothing_configured_the_office_gets_a_loud_error_never_a_silent_guess()
    {
        var sut = Build(office: null, local: null, systemDefault: null, providers: []);

        var ex = await Assert.ThrowsAsync<ModelUnavailableException>(() => sut.CompleteAsync(Request("dispatcher")));
        Assert.Contains("Sistem varsayılan modeli", ex.Message);
    }

    [Fact]
    public async Task A_selection_naming_an_unregistered_provider_fails_clearly()
    {
        var sut = Build(
            office: null,
            local: null,
            systemDefault: new OfficeModelOverride("gpt", "gpt-4o", null, ModelSelectionSource.SystemDefault, "gpt-4o"),
            providers: []); // "gpt" never registered

        await Assert.ThrowsAsync<ModelUnavailableException>(() => sut.CompleteAsync(Request("dispatcher")));
    }

    private static ChatRequest Request(string specialty) => new("system prompt", [], specialty);

    private static RoutingChatModelService Build(
        OfficeModelOverride? office, ModelSelection? local, OfficeModelOverride? systemDefault,
        IReadOnlyList<IChatProviderService> providers)
        => new(
            new FakeRouter(local), new FakeOverrides(office, systemDefault), new FakeCurrentUser(),
            providers);

    private sealed class FakeRouter(ModelSelection? local) : IModelRouterService
    {
        public ModelSelection? ResolveLocal(string specialty) => local;
    }

    private sealed class FakeOverrides(OfficeModelOverride? office, OfficeModelOverride? systemDefault) : IOfficeModelOverrideService
    {
        public Task<OfficeModelOverride?> ResolveOfficeAsync(Guid officeId, CancellationToken ct = default)
            => Task.FromResult(officeId == OfficeId ? office : null);

        public Task<OfficeModelOverride?> ResolveSystemDefaultAsync(CancellationToken ct = default)
            => Task.FromResult(systemDefault);
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public Guid OfficeId => RoutingChatModelServiceTests.OfficeId;

        public Guid UserId => Guid.Empty;

        public IReadOnlySet<string> Permissions => new HashSet<string>();
    }

    /// <summary>Echoes the provider/model it was called with, so a test can assert who actually won.</summary>
    private sealed class FakeProvider(string name) : IChatProviderService
    {
        public string Name => name;

        public Task<ChatReply> CompleteAsync(ModelSelection selection, ChatRequest request, CancellationToken ct = default)
            => Task.FromResult(new ChatReply("ok", new ChatUsage(selection.Provider, selection.ModelId, 1, 1)));

        public async IAsyncEnumerable<ChatStreamEvent> StreamAsync(
            ModelSelection selection, ChatRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            await Task.CompletedTask;
            yield return ChatStreamEvent.Final(new ChatUsage(selection.Provider, selection.ModelId, 1, 1));
        }
    }
}
