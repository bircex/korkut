using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Contracts;

public sealed record PendingEditResponse(Guid Id, string ContentMd, string? Summary);
