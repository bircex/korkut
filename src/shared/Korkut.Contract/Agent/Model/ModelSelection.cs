namespace Korkut.Contract.Agent.Model;

/// <summary>A resolved provider + concrete model + token cap for one call (the router's output).</summary>
public sealed record ModelSelection(string Provider, string ModelId, int MaxTokens);
