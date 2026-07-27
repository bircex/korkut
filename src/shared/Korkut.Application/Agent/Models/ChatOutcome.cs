using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Models;

/// <summary>The result of a chat turn: the assistant reply, the (possibly auto-set) title, and UI hints.</summary>
public sealed record ChatOutcome(string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail);
