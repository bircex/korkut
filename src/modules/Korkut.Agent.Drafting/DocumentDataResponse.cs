namespace Korkut.Agent.Drafting;

public sealed record DocumentDataResponse(
    Guid Id, string Title, string ContentMd, string Status, IReadOnlyList<DocVersionResponse> Versions, PendingEditResponse? Pending);
