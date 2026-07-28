namespace Korkut.Contract.Documents;

/// <summary>One extracted span with its page + order, for provenance-tracked projections.</summary>
public sealed record TextExtractorServiceResult(int Page, int Index, string Text);
