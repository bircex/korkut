using Korkut.Contract.Agent.Model;
using Korkut.Organization.Services;

namespace Korkut.Organization.Unit.Tests;

/// <summary>
/// The dynamic model plane's two building blocks — resolve an office's own pin, resolve the
/// system-wide default — each as a pure function of a loaded snapshot. This is what decides which
/// LLM every chat turn in the product reaches (see RoutingChatModelServiceTests for how the caller
/// merges these with the local/hearing specialty decision into the full precedence chain).
/// </summary>
public sealed class OfficeModelResolverTests
{
    private static readonly Guid Office = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherOffice = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static ModelPlaneSnapshot Snapshot(
        string? defaultKey = null,
        IEnumerable<CatalogRow>? catalog = null,
        IEnumerable<KeyValuePair<Guid, string>>? assignments = null)
        => new(
            DateTimeOffset.UnixEpoch,
            (catalog ?? DefaultCatalog).ToDictionary(c => c.Key, StringComparer.Ordinal),
            defaultKey,
            (assignments ?? []).ToDictionary(a => a.Key, a => a.Value));

    private static readonly CatalogRow[] DefaultCatalog =
    [
        new("deepseek-v3", "DeepSeek V3", ChatProviders.DeepSeek, "deepseek-chat", 8000, Enabled: true),
        new("gemini", "Gemini", ChatProviders.Gemini, "gemini-3.5-flash", 4000, Enabled: true),
        new("retired", "Retired", ChatProviders.Claude, "claude-old", null, Enabled: false),
    ];

    // --- ResolveOffice ---

    [Fact]
    public void No_assignment_resolves_to_null()
    {
        Assert.Null(OfficeModelResolver.ResolveOffice(Snapshot(), Office));
    }

    [Fact]
    public void An_assignment_only_binds_the_office_it_names()
    {
        var snapshot = Snapshot(assignments: [new(OtherOffice, "deepseek-v3")]);

        Assert.Null(OfficeModelResolver.ResolveOffice(snapshot, Office));
        Assert.Equal("deepseek-v3", OfficeModelResolver.ResolveOffice(snapshot, OtherOffice)!.ModelKey);
    }

    [Fact]
    public void An_office_assignment_reports_the_office_source()
    {
        var selection = OfficeModelResolver.ResolveOffice(
            Snapshot(assignments: [new(Office, "gemini")]), Office);

        Assert.NotNull(selection);
        Assert.Equal(ChatProviders.Gemini, selection.Provider);
        Assert.Equal("gemini-3.5-flash", selection.ModelId);
        Assert.Equal(4000, selection.MaxTokens);
        Assert.Equal(ModelSelectionSource.Office, selection.Source);
    }

    [Fact]
    public void A_disabled_assigned_model_resolves_to_null_not_a_dead_selection()
    {
        Assert.Null(OfficeModelResolver.ResolveOffice(
            Snapshot(assignments: [new(Office, "retired")]), Office));
    }

    // --- ResolveSystemDefault ---

    [Fact]
    public void An_unset_default_resolves_to_null()
    {
        Assert.Null(OfficeModelResolver.ResolveSystemDefault(Snapshot()));
    }

    [Fact]
    public void A_configured_default_reports_the_system_default_source()
    {
        var selection = OfficeModelResolver.ResolveSystemDefault(Snapshot(defaultKey: "gemini"));

        Assert.NotNull(selection);
        Assert.Equal("gemini", selection.ModelKey);
        Assert.Equal(ModelSelectionSource.SystemDefault, selection.Source);
    }

    [Fact]
    public void A_default_pointing_at_a_missing_catalog_entry_resolves_to_null()
    {
        Assert.Null(OfficeModelResolver.ResolveSystemDefault(Snapshot(defaultKey: "deleted-model")));
    }

    [Fact]
    public void A_default_pointing_at_a_disabled_entry_resolves_to_null()
    {
        Assert.Null(OfficeModelResolver.ResolveSystemDefault(Snapshot(defaultKey: "retired")));
    }

    [Fact]
    public void An_entry_naming_an_unknown_provider_is_never_routed_to()
    {
        var snapshot = Snapshot(
            defaultKey: "bogus",
            catalog: [new("bogus", "Bogus", "not-a-provider", "x", null, Enabled: true)]);

        Assert.Null(OfficeModelResolver.ResolveSystemDefault(snapshot));
    }

    [Fact]
    public void A_null_max_tokens_is_carried_through_so_the_caller_default_applies()
    {
        var snapshot = Snapshot(
            defaultKey: "no-cap",
            catalog: [new("no-cap", "No cap", ChatProviders.Groq, "llama", null, Enabled: true)]);

        Assert.Null(OfficeModelResolver.ResolveSystemDefault(snapshot)!.MaxTokens);
    }
}
