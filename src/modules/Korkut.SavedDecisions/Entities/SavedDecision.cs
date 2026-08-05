using Korkut.Core;

namespace Korkut.SavedDecisions.Entities;

/// <summary>
/// One decision an office bookmarked from the public legal databases (Karar Arama /
/// İlgili Kararlar). A denormalized copy of the search hit's metadata, so the list renders
/// without re-fetching the source; <see cref="Source"/>+<see cref="ExternalId"/> is what
/// ILawClientsDocumentService.GetAsync accepts to re-fetch the full text.
/// </summary>
public sealed class SavedDecision : Entity, ITenantOwned, IAggregateRoot
{
    public SavedDecision(
        Guid officeId, Guid createdByUserId, string source, string externalId, string title,
        string? date, string? caseNumber, string? decisionNumber, string? court, string? chamber,
        string? url, string? snippet, DateTimeOffset createdAt)
        : base(NewId())
    {
        OfficeId = officeId;
        CreatedByUserId = createdByUserId;
        Source = source;
        ExternalId = externalId;
        Title = title;
        Date = date;
        CaseNumber = caseNumber;
        DecisionNumber = decisionNumber;
        Court = court;
        Chamber = chamber;
        Url = url;
        Snippet = snippet;
        CreatedAt = createdAt;
    }

    /// <summary>Materialization constructor for the ORM.</summary>
    private SavedDecision()
    {
    }

    public Guid OfficeId { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string Source { get; private set; } = null!;

    public string ExternalId { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public string? Date { get; private set; }

    public string? CaseNumber { get; private set; }

    public string? DecisionNumber { get; private set; }

    public string? Court { get; private set; }

    public string? Chamber { get; private set; }

    public string? Url { get; private set; }

    public string? Snippet { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
