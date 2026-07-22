namespace Korkut.Agent.Services.Implementations.Ai;

/// <summary>The feature (call site) that issued a model call — a span/telemetry dimension.</summary>
public static class LlmFeature
{
    public const string Chat = "chat";

    public const string Drafting = "drafting";
}
