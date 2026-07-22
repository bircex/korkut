namespace Korkut.Agent.Services.Implementations.Ai;

/// <summary>
/// The trusted identity a tool call runs under: the office (tenant) it is pinned to, the acting
/// user, and that user's effective permission set (for tool RBAC). Built from the access token at
/// the edge — never from a request body.
/// </summary>
public sealed record AgentToolContext(Guid OfficeId, Guid UserId, IReadOnlySet<string> Permissions);
