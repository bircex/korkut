using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Models;

/// <summary>The feature (call site) that issued a model call — a span/telemetry dimension.</summary>
public static class LlmFeature
{
    public const string Chat = "chat";

    public const string Drafting = "drafting";
}
