using Korkut.Agent.Application;
using Korkut.Application.Runtime;
using Korkut.Application.Security;

namespace Korkut.Application.Tests;

/// <summary>
/// Env / model / provider / tool compatibility parity with the Python baseline
/// (KDM-033). Verifies exact names + defaults, provider selection + fallback,
/// malformed-config fail-fast, secret redaction, and that the stale
/// KORKUT_MODELS_FILE / KORKUT_SUPERADMIN_EMAILS variables stay inert.
/// </summary>
public class RuntimeCompatibilityTests
{
    private sealed class FakeEnv(Dictionary<string, string?> vars) : IEnvironment
    {
        public string? Get(string name) => vars.TryGetValue(name, out var v) ? v : null;
    }

    private static RuntimeConfig Config(Dictionary<string, string?>? vars = null) => new(new FakeEnv(vars ?? new()));

    // The real routing config, kept in sync with config/model-routing.yaml.
    private const string RoutingYaml = """
        backends:
          cloud: groq
          local: ollama
        fallback_to_cloud_if_no_local: true
        specialties:
          dispatcher: { model: cloud }
          hearing:    { model: local }
          petition:   { model: cloud }
          research:   { model: cloud }
        """;

    [Fact]
    public void Provider_model_defaults_match_the_python_baseline_exactly()
    {
        var c = Config();
        Assert.Equal("claude-opus-4-8", c.AnthropicModel);
        Assert.Equal(16000, c.AnthropicMaxTokens);
        Assert.Equal("gpt-4o", c.OpenAiModel);
        Assert.Equal("llama-3.3-70b-versatile", c.GroqModel);
        Assert.Equal(128000, c.ContextWindow);
        Assert.Equal(8192, c.LocalContextWindow);
        Assert.Equal(1024, c.ReplyHeadroom);
    }

    [Fact]
    public void Env_overrides_are_honoured_under_their_exact_names()
    {
        var c = Config(new()
        {
            ["ANTHROPIC_MODEL"] = "claude-sonnet-5",
            ["ANTHROPIC_MAX_TOKENS"] = "8000",
            ["OPENAI_MODEL"] = "gpt-5",
            ["GROQ_MODEL"] = "llama-4",
            ["KORKUT_CONTEXT_WINDOW"] = "200000",
        });
        Assert.Equal("claude-sonnet-5", c.AnthropicModel);
        Assert.Equal(8000, c.AnthropicMaxTokens);
        Assert.Equal("gpt-5", c.OpenAiModel);
        Assert.Equal("llama-4", c.GroqModel);
        Assert.Equal(200000, c.ContextWindow);
    }

    [Fact]
    public void A_malformed_numeric_env_fails_fast_like_python_int()
    {
        var c = Config(new() { ["ANTHROPIC_MAX_TOKENS"] = "not-a-number" });
        Assert.Throws<FormatException>(() => c.AnthropicMaxTokens);
    }

    [Fact]
    public void Api_keys_are_exposed_only_as_presence_never_as_values()
    {
        var c = Config(new() { ["ANTHROPIC_API_KEY"] = "sk-ant-secret", ["OPENAI_API_KEY"] = "" });
        Assert.True(c.HasAnthropicKey);
        Assert.False(c.HasOpenAiKey); // empty == unset
        Assert.False(c.HasGroqKey);
        // The RuntimeConfig surface has no property that returns a key value.
        var props = typeof(RuntimeConfig).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain(props, n => n.Contains("Key", StringComparison.Ordinal) && !n.StartsWith("Has", StringComparison.Ordinal));
    }

    [Fact]
    public void Model_routing_parses_and_selects_providers_like_python()
    {
        var routing = ModelRoutingLoader.Parse(RoutingYaml);
        var providers = new HashSet<string> { "groq", "ollama", "claude", "gpt" };

        // Listed specialties resolve to their tier; an unlisted one uses the default (cloud).
        Assert.Equal("cloud", routing.ResolveTier("dispatcher", localAvailable: true));
        Assert.Equal("local", routing.ResolveTier("hearing", localAvailable: true));
        Assert.Equal("cloud", routing.ResolveTier("unknown-specialty", localAvailable: true));

        // Provider mapping.
        Assert.Equal("groq", routing.ProviderForTier("cloud", providers));
        Assert.Equal("ollama", routing.ProviderForTier("local", providers));
    }

    [Fact]
    public void Local_unavailable_falls_back_to_cloud_only_when_configured()
    {
        var withFallback = ModelRoutingLoader.Parse(RoutingYaml);
        Assert.Equal("cloud", withFallback.ResolveTier("hearing", localAvailable: false));

        var noFallback = ModelRoutingLoader.Parse(RoutingYaml.Replace("fallback_to_cloud_if_no_local: true", "fallback_to_cloud_if_no_local: false"));
        Assert.Throws<InvalidOperationException>(() => noFallback.ResolveTier("hearing", localAvailable: false));
    }

    [Fact]
    public void An_unregistered_provider_for_a_tier_is_a_hard_error()
    {
        var routing = ModelRoutingLoader.Parse(RoutingYaml);
        var onlyCloud = new HashSet<string> { "groq" };
        Assert.Throws<InvalidOperationException>(() => routing.ProviderForTier("local", onlyCloud)); // ollama not registered
    }

    [Theory]
    [InlineData("")]
    [InlineData("specialties: { a: { model: cloud } }")] // no backends
    [InlineData(": : not yaml : :")]
    public void Malformed_or_incomplete_routing_config_fails_fast(string yaml)
    {
        Assert.ThrowsAny<Exception>(() => ModelRoutingLoader.Parse(yaml));
    }

    [Fact]
    public void KorkutModelsFile_is_inert_even_when_set()
    {
        var c = Config(new() { ["KORKUT_MODELS_FILE"] = "/etc/korkut/models.json" });
        Assert.False(c.ModelsFileIsActive); // parity: the Python runtime never reads it
    }

    [Fact]
    public void KorkutSuperadminEmails_grants_nothing_and_the_allowlist_is_compiled_in()
    {
        var c = Config(new() { ["KORKUT_SUPERADMIN_EMAILS"] = "attacker@evil.com" });
        Assert.False(c.SuperadminEnvIsActive);
        // The env value never appears in the compiled allowlist.
        Assert.False(AdminAllowlist.IsAllowed("attacker@evil.com"));
    }

    [Fact]
    public void Config_redaction_masks_secret_named_values_and_keeps_the_rest()
    {
        var env = new Dictionary<string, string>
        {
            ["ANTHROPIC_API_KEY"] = "sk-ant-abc123",
            ["KORKUT_SMTP_PASSWORD"] = "hunter2",
            ["GOOGLE_CLIENT_SECRET"] = "gsecret",
            ["ANTHROPIC_MODEL"] = "claude-opus-4-8",
            ["KORKUT_PORT"] = "8080",
        };
        var red = ConfigRedactor.RedactMap(env);

        Assert.Equal(ConfigRedactor.Mask, red["ANTHROPIC_API_KEY"]);
        Assert.Equal(ConfigRedactor.Mask, red["KORKUT_SMTP_PASSWORD"]);
        Assert.Equal(ConfigRedactor.Mask, red["GOOGLE_CLIENT_SECRET"]);
        Assert.Equal("claude-opus-4-8", red["ANTHROPIC_MODEL"]); // model ids are not secret
        Assert.Equal("8080", red["KORKUT_PORT"]);
        // No secret value survives anywhere in the redacted output.
        Assert.DoesNotContain(red.Values, v => v is "sk-ant-abc123" or "hunter2" or "gsecret");
    }

    // The tool key-gating (ported from the Python tool_registry) now lives in the Agent host
    // (Korkut.Agent.Tools.ToolCatalog) with all other tool code, and is covered by
    // Korkut.Agent.Tests.ToolCatalogTests — a tool is an agent concern, not an Application one.
}
