using System.Reflection;
using NetArchTest.Rules;

namespace Korkut.Architecture.Tests;

/// <summary>
/// Enforces the one-way dependency flow of the agent layering (docs/AGENT_ARCHITECTURE.md):
/// data (Ledger) &lt;- capabilities (Model, Tools) &lt;- reasoning (Brain) &lt;- use-cases
/// (Chat, Drafting). An arrow only ever points DOWN; a layer never references a layer above it,
/// and the layers stay free of Infrastructure/host. Fails the build if the flow is violated.
/// </summary>
public class AgentLayerDependencyTests
{
    private static readonly Assembly Tools = typeof(Korkut.Tools.ToolCatalog).Assembly;
    private static readonly Assembly Model = typeof(Korkut.Agent.Model.Extensions.AgentModelSetup).Assembly;
    private static readonly Assembly Brain = typeof(Korkut.Agent.Brain.Extensions.AgentBrainSetup).Assembly;
    private static readonly Assembly Chat = typeof(Korkut.Agent.Chat.Extensions.AgentChatSetup).Assembly;
    private static readonly Assembly Drafting = typeof(Korkut.Agent.Drafting.Extensions.AgentDraftingSetup).Assembly;
    private static readonly Assembly Ledger = typeof(Korkut.Agent.Ledger.ILlmCallRepository).Assembly;

    private static void AssertNoDependency(Assembly assembly, params string[] forbidden)
    {
        var result = Types.InAssembly(assembly).ShouldNot().HaveDependencyOnAny(forbidden).GetResult();
        Assert.True(result.IsSuccessful,
            $"{assembly.GetName().Name} must not depend on [{string.Join(", ", forbidden)}]. " +
            $"Offenders: {string.Join(", ", result.FailingTypeNames ?? Array.Empty<string>())}");
    }

    private const string Infra = "Korkut.Infrastructure";
    private const string M = "Korkut.Agent.Model";
    private const string B = "Korkut.Agent.Brain";
    private const string C = "Korkut.Agent.Chat";
    private const string D = "Korkut.Agent.Drafting";
    private const string T = "Korkut.Tools";

    [Fact]
    public void Tools_is_a_capability_leaf() => AssertNoDependency(Tools, M, B, C, D, Infra);

    [Fact]
    public void Ledger_is_a_data_leaf() => AssertNoDependency(Ledger, T, M, B, C, D, Infra);

    [Fact]
    public void Model_does_not_depend_on_reasoning_or_use_cases_or_tools() => AssertNoDependency(Model, T, B, C, D, Infra);

    [Fact]
    public void Brain_does_not_depend_on_the_use_cases() => AssertNoDependency(Brain, C, D, Infra);

    [Fact]
    public void Chat_does_not_depend_on_drafting_or_infrastructure() => AssertNoDependency(Chat, D, Infra);

    [Fact]
    public void Drafting_does_not_depend_on_infrastructure() => AssertNoDependency(Drafting, Infra);
}
