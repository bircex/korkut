using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Models;

/// <summary>A resolved provider + concrete model + token cap for one call (the router's output).</summary>
public sealed record ModelSelection(string Provider, string ModelId, int MaxTokens);
