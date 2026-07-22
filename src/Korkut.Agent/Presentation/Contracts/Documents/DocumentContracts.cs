using System.ComponentModel.DataAnnotations;

namespace Korkut.Agent.Presentation.Contracts.Documents;

public sealed record DocVersionResponse(Guid Id, string? Author, string? Note, DateTimeOffset CreatedAt);

public sealed record PendingEditResponse(Guid Id, string ContentMd, string? Summary);

public sealed record DocumentDataResponse(
    Guid Id, string Title, string ContentMd, string Status, IReadOnlyList<DocVersionResponse> Versions, PendingEditResponse? Pending);

public sealed record DraftRequest
{
    [Required, MaxLength(4000)]
    public string? Instruction { get; init; }

    [MaxLength(100)]
    public string? PetitionType { get; init; }
}

public sealed record SaveDocumentRequest
{
    [Required, MaxLength(300)]
    public string? Title { get; init; }

    [Required]
    public string? ContentMd { get; init; }
}
