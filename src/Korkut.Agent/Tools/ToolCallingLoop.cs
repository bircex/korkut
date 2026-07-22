using Korkut.Agent.Application.Ai;
using Korkut.Agent.Application.Abstractions.Ai;
using Korkut.Agent.Application.Abstractions.Persistence;
using Korkut.Agent.Application.Abstractions.Sessions;
using Korkut.Agent.Application.Sessions;
using Korkut.Agent.Application.Chat;

namespace Korkut.Agent.Tools;

/// <summary>
/// The assistant's tool-calling loop. It offers the CALLER'S permitted tools to the model (the
/// catalog filters by key + RBAC), runs any tool the model calls — office-scoped (RLS) and
/// permission-checked via the catalog — feeds the results back, and repeats until the model returns
/// a final text answer or the iteration cap is reached. The model, not the loop, decides which tools
/// to call; adding a tool never changes this loop. Implements <see cref="IAgentToolLoop"/> so lower
/// layers (the chat service) can drive a tool-augmented turn without depending on the tools.
/// </summary>
public sealed class ToolCallingLoop(IChatModel llm, IToolCatalog catalog) : IAgentToolLoop
{
    private const int MaxIterations = 8;

    /// <summary>Port entry point: build the tenant/RBAC tool context and run the loop.</summary>
    public Task<ChatReply> RunAsync(
        string system, IReadOnlyList<ChatMessage> messages, AgentToolContext context,
        string specialty, int? maxTokens = null,
        Func<string, Task>? onStep = null, Func<string, Task>? onToken = null, CancellationToken ct = default)
        => CoreAsync(system, messages, new ToolContext(context.OfficeId, context.UserId, context.Permissions),
            specialty, maxTokens, onStep, onToken, ct);

    /// <summary>Convenience overload used by unit tests: returns just the final text.</summary>
    public async Task<string> RunAsync(
        string system, IReadOnlyList<ChatMessage> history, ToolContext context,
        string specialty, int? maxTokens = null, CancellationToken ct = default)
    {
        var reply = await CoreAsync(system, history, context, specialty, maxTokens, null, null, ct);
        return reply.Text;
    }

    private async Task<ChatReply> CoreAsync(
        string system, IReadOnlyList<ChatMessage> history, ToolContext context,
        string specialty, int? maxTokens, Func<string, Task>? onStep, Func<string, Task>? onToken, CancellationToken ct)
    {
        var tools = catalog.AvailableSpecs(context)
            .Select(s => new ToolDefinition(s.Name, s.Description, s.Parameters))
            .ToList();

        var messages = history.ToList();
        var prompt = 0;
        var completion = 0;
        var provider = string.Empty;
        var modelId = string.Empty;

        for (var i = 0; i < MaxIterations; i++)
        {
            var reply = await llm.CompleteAsync(new ChatRequest(system, messages, specialty, maxTokens, Tools: tools), ct);
            (prompt, completion, provider, modelId) = Accumulate(prompt, completion, reply.Usage);

            if (reply.ToolCalls is not { Count: > 0 } calls)
            {
                if (onToken is not null && reply.Text.Length > 0)
                {
                    await onToken(reply.Text);
                }
                return reply with { Usage = new ChatUsage(provider, modelId, prompt, completion) };
            }

            // Record the assistant's tool-call turn, then run each call and feed the results back.
            messages.Add(new ChatMessage("assistant", reply.Text, ToolCalls: calls));
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

            messages.Add(new ChatMessage("tool", string.Empty, ToolResults: outputs));
        }

        // Iteration cap reached — ask once more WITHOUT tools to force a final answer.
        var final = await llm.CompleteAsync(new ChatRequest(system, messages, specialty, maxTokens), ct);
        (prompt, completion, provider, modelId) = Accumulate(prompt, completion, final.Usage);
        if (onToken is not null && final.Text.Length > 0)
        {
            await onToken(final.Text);
        }
        return final with { Usage = new ChatUsage(provider, modelId, prompt, completion) };
    }

    // Sum token counts across the loop's model calls; keep the last call's provider/model for the label.
    private static (int Prompt, int Completion, string Provider, string ModelId) Accumulate(int prompt, int completion, ChatUsage usage)
        => (prompt + usage.PromptTokens, completion + usage.CompletionTokens,
            string.IsNullOrEmpty(usage.Provider) ? string.Empty : usage.Provider,
            string.IsNullOrEmpty(usage.ModelId) ? string.Empty : usage.ModelId);
}
