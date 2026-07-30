using Korkut.Agent.Brain.Abstractions;
using Korkut.Tools.Core;
using Korkut.Contract.Agent.Model;
using System.Text;

namespace Korkut.Agent.Brain.Services;

/// <summary>
/// The assistant's tool-calling loop. It offers the CALLER'S permitted tools to the model (the
/// catalog filters by key + RBAC), runs any tool the model calls — office-scoped (RLS) and
/// permission-checked via the catalog — feeds the results back, and repeats until the model returns
/// a final text answer or the iteration cap is reached. The model, not the loop, decides which tools
/// to call; adding a tool never changes this loop. Implements <see cref="IAgentToolLoopService"/> so
/// lower layers (the chat service) can drive a tool-augmented turn without depending on the tools.
/// </summary>
internal sealed class ToolCallingLoopService(IChatModelService llm, IToolCatalog catalog) : IAgentToolLoopService
{
    private const int MaxIterations = 8;

    public async Task<ChatReply> RunAsync(
        string system, IReadOnlyList<ChatMessage> messages, AgentToolContext context,
        string specialty, int? maxTokens = null,
        Func<string, Task>? onStep = null, Func<string, Task>? onToken = null, CancellationToken ct = default)
    {
        var tools = catalog.AvailableSpecs(context)
            .Select(s => new ToolDefinition(s.Name, s.Description, s.Parameters))
            .ToList();

        var turn = messages.ToList();
        var prompt = 0;
        var completion = 0;
        var provider = string.Empty;
        var modelId = string.Empty;

        for (var i = 0; i < MaxIterations; i++)
        {
            // Stream the turn: text deltas reach the user token-by-token via onToken as they arrive,
            // and any tool calls are collected to run before the next turn.
            var (text, toolCalls, usage) = await StreamTurnAsync(system, turn, specialty, maxTokens, tools, onToken, ct);
            (prompt, completion, provider, modelId) = Accumulate(prompt, completion, usage);

            if (toolCalls is not { Count: > 0 } calls)
            {
                return new ChatReply(text, new ChatUsage(provider, modelId, prompt, completion));
            }

            // Record the assistant's tool-call turn, then run each call and feed the results back.
            turn.Add(new ChatMessage("assistant", text, ToolCalls: calls));
            var outputs = new List<ToolOutput>(calls.Count);
            foreach (var call in calls)
            {
                if (onStep is not null)
                {
                    await onStep($"Araç çalıştırılıyor: {call.Name}");
                }

                var result = await catalog.InvokeAsync(call.Name, call.Arguments, context, ct);
                outputs.Add(new ToolOutput(call.Id, result.Content, result.IsError));
            }

            turn.Add(new ChatMessage("tool", string.Empty, ToolResults: outputs));
        }

        // Iteration cap reached — ask once more (streamed) WITHOUT tools to force a final answer.
        var (finalText, _, finalUsage) = await StreamTurnAsync(system, turn, specialty, maxTokens, tools: null, onToken, ct);
        (prompt, completion, provider, modelId) = Accumulate(prompt, completion, finalUsage);
        return new ChatReply(finalText, new ChatUsage(provider, modelId, prompt, completion));
    }

    // Drive one streamed model turn: append text deltas (live via onToken), collect any tool calls,
    // and capture usage. The provider emits a single tool-calls event once the turn is a tool-use turn.
    private async Task<(string Text, IReadOnlyList<ToolInvocation>? ToolCalls, ChatUsage Usage)> StreamTurnAsync(
        string system, IReadOnlyList<ChatMessage> turn, string specialty, int? maxTokens,
        IReadOnlyList<ToolDefinition>? tools, Func<string, Task>? onToken, CancellationToken ct)
    {
        var text = new StringBuilder();
        IReadOnlyList<ToolInvocation>? toolCalls = null;
        var usage = ChatUsage.None;

        await foreach (var ev in llm.StreamAsync(new ChatRequest(system, turn, specialty, maxTokens, Tools: tools), ct))
        {
            if (ev.Delta is { Length: > 0 } delta)
            {
                text.Append(delta);
                if (onToken is not null)
                {
                    await onToken(delta);
                }
            }

            if (ev.ToolCalls is { Count: > 0 } calls)
            {
                toolCalls = calls;
            }

            if (ev.Usage is { } u)
            {
                usage = u;
            }
        }

        return (text.ToString(), toolCalls, usage);
    }

    // Sum token counts across the loop's model calls; keep the last call's provider/model for the label.
    private static (int Prompt, int Completion, string Provider, string ModelId) Accumulate(
        int prompt, int completion, ChatUsage usage)
        => (prompt + usage.PromptTokens, completion + usage.CompletionTokens, usage.Provider, usage.ModelId);
}
