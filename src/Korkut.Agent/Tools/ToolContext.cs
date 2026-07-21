namespace Korkut.Agent.Tools;

/// <summary>
/// The office-scoped context every tool call runs in. Tenant (<see cref="OfficeId"/>) is
/// MANDATORY — a tool always acts within one office (RLS); it is never inferred from arguments.
/// </summary>
public sealed record ToolContext(Guid OfficeId, Guid UserId);
