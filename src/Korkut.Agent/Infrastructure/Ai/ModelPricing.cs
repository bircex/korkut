namespace Korkut.Agent.Infrastructure.Ai;

/// <summary>
/// Approximate list prices (USD per 1,000,000 tokens, input/output) for the pinned models, used to
/// derive a per-call cost for the <c>llm_calls</c> ledger. Config-as-code, like the routing rules;
/// an unknown model costs 0 (recorded honestly rather than guessed). A local/on-prem model is free.
/// </summary>
public static class ModelPricing
{
    private static readonly Dictionary<string, (decimal Input, decimal Output)> Prices =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["claude-opus-4-8"] = (15m, 75m),
            ["gpt-4o"] = (2.5m, 10m),
            ["llama-3.3-70b-versatile"] = (0.59m, 0.79m),
        };

    public static decimal Cost(string modelId, int promptTokens, int completionTokens)
    {
        if (string.IsNullOrEmpty(modelId) || !Prices.TryGetValue(modelId, out var price))
        {
            return 0m;
        }

        return (promptTokens / 1_000_000m * price.Input) + (completionTokens / 1_000_000m * price.Output);
    }
}
