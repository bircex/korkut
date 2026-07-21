using Korkut.Application.Ai;

namespace Korkut.Agent.Tools;

/// <summary>
/// The assistant's tool-calling loop. It offers the CALLER'S permitted tools to the model (the
/// catalog filters by key + RBAC), runs any tool the model calls — office-scoped (RLS) and
/// permission-checked via the catalog — feeds the results back, and repeats until the model returns
/// a final text answer or the iteration cap is reached. The model, not the loop, decides which tools
/// to call; adding a tool never changes this loop.
/// </summary>
public sealed class ToolCallingLoop(IChatModel llm, IToolCatalog catalog)
{
    public const int MaxIterations = 8;

    public async Task<string> RunAsync(
        string system, IReadOnlyList<ChatMessage> history, ToolContext context,
        string specialty, int? maxTokens = null, CancellationToken ct = default)
    {
        var tools = catalog.AvailableSpecs(context)
            .Select(s => new ToolDefinition(s.Name, s.Description, s.Parameters))
            .ToList();

        var messages = history.ToList();

        for (var i = 0; i < MaxIterations; i++)
        {
            var reply = await llm.CompleteAsync(new ChatRequest(system, messages, specialty, maxTokens, Tools: tools), ct);
            if (reply.ToolCalls is not { Count: > 0 } calls)
            {
                return reply.Text; // the model answered
            }

            // Record the assistant's tool-call turn, then run each call and feed the results back.
            messages.Add(new ChatMessage("assistant", reply.Text, ToolCalls: calls));
            var outputs = new List<ToolOutput>(calls.Count);
            foreach (var call in calls)
            {
                var result = await catalog.InvokeAsync(call.Name, call.Arguments, context, ct);
                outputs.Add(new ToolOutput(call.Id, result.Content, result.IsError));
            }

            messages.Add(new ChatMessage("tool", string.Empty, ToolResults: outputs));
        }

        // Iteration cap reached — ask once more WITHOUT tools to force a final answer.
        var final = await llm.CompleteAsync(new ChatRequest(system, messages, specialty, maxTokens), ct);
        return final.Text;
    }
}
