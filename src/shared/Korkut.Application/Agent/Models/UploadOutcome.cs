using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Models;

/// <summary>The result of a file-upload turn: the assistant reply, the title, and a display line for the user turn.</summary>
public sealed record UploadOutcome(string Reply, string Title, string UserDisplay);
