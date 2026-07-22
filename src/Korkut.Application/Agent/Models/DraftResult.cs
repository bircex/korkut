using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Models;

/// <summary>A staged draft edit produced by the assistant: the pending version id + its content/summary.</summary>
public sealed record DraftResult(Guid PendingId, string ContentMd, string Summary);
