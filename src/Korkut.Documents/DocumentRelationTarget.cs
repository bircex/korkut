namespace Korkut.Documents;

/// <summary>The kinds of record a document may be filed against.</summary>
public static class DocumentRelationTarget
{
    public const string Client = "client";
    public const string Matter = "matter";
    public const string Party = "party";

    public static bool IsValid(string? type) => type is Client or Matter or Party;
}
