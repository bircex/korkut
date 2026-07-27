using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Contracts;

public sealed record ChatResponse(string Reply, string Title, Guid? OpenDocument, Guid? PendingEmail);
