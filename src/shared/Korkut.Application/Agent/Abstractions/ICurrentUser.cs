using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Abstractions;

/// <summary>
/// The authenticated caller for the current request: the office (tenant) they act in, their user id,
/// and their effective permission set (for tool RBAC). Resolved from the access token at the edge,
/// so application services never touch the HTTP context or a request body to learn who is calling.
/// </summary>
public interface ICurrentUser
{
    Guid OfficeId { get; }

    Guid UserId { get; }

    IReadOnlySet<string> Permissions { get; }
}
