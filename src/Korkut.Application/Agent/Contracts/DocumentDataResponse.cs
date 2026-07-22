using Korkut.Application.Agent.Abstractions;
using Korkut.Application.Agent.Models;
namespace Korkut.Application.Agent.Contracts;

public sealed record DocumentDataResponse(
    Guid Id, string Title, string ContentMd, string Status, IReadOnlyList<DocVersionResponse> Versions, PendingEditResponse? Pending);
