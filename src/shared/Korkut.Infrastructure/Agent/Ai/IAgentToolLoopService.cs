using Korkut.Tools.Tools;
using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;

namespace Korkut.Infrastructure.Agent.Ai;

/// <summary>
/// Runs the assistant's tool-calling loop: offers the caller's permitted tools to the model, runs
/// any tool it calls (office-scoped + permission-checked), feeds results back, and repeats until a
/// final answer. Implemented in the Agent host (where the tools live) so lower layers can drive a
/// tool-augmented turn without depending on the tool implementations.
/// </summary>
public interface IAgentToolLoopService
{
    /// <summary>
    /// Drive a full turn. <paramref name="onStep"/> receives short activity labels (tool calls);
    /// <paramref name="onToken"/> receives the final answer text. Returns the final reply, whose
    /// <see cref="ChatReply.Usage"/> aggregates every model call the loop made.
    /// </summary>
    Task<ChatReply> RunAsync(
        string system, IReadOnlyList<ChatMessage> messages, AgentToolContext context,
        string specialty, int? maxTokens = null,
        Func<string, Task>? onStep = null, Func<string, Task>? onToken = null, CancellationToken ct = default);
}
