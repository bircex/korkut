using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Contracts;

public sealed record SessionDetailResponse(
    Guid Id, string Title, Guid? MatterId, string? CreatedByName, DateTimeOffset? UpdatedAt, IReadOnlyList<ChatMessageResponse> Messages);
