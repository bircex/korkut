using System.Text.Json;

namespace Korkut.Tools.Law;

/// <summary>
/// One public Turkish legal database (Yargıtay, Danıştay, Emsal, Mevzuat, Anayasa, GİB, KİK, …). The
/// composite <c>search_decisions</c> / <c>fetch_document</c> tools dispatch to the client whose
/// <see cref="Source"/> matches the requested source. A client may need an API key
/// (<see cref="RequiresKey"/>, e.g. Tavily-backed sources); when unset that source is unavailable.
/// Raw JSON args are forwarded so each client reads only the filters it supports.
/// </summary>
public interface ISourceClient
{
    /// <summary>The source token the model selects (e.g. "yargitay", "danistay", "kvkk").</summary>
    string Source { get; }

    /// <summary>The env var this client needs (e.g. TAVILY_API_KEY); null = no key needed.</summary>
    string? RequiresKey { get; }

    /// <summary>True if this source supports fetching a full document by id.</summary>
    bool SupportsFetch { get; }

    Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default);

    /// <summary>
    /// Search plus the database's TOTAL match count when the source reports one — what lets the
    /// assistant say "bu sorguya 2.894 karar uydu; en ilgili 10'unu inceledim" honestly. Default
    /// wraps <see cref="SearchAsync"/> with an unknown total; sources that get a count override it.
    /// </summary>
    async Task<SearchOutcome> SearchPagedAsync(string query, JsonElement args, CancellationToken ct = default)
        => new(await SearchAsync(query, args, ct), Total: null);

    /// <summary>Full document text (Markdown) for an id from this source; null if unsupported/not found.</summary>
    Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default);
}

/// <summary>A page of search hits plus the database-reported total match count (null = unknown).</summary>
public sealed record SearchOutcome(IReadOnlyList<SourceResult> Items, long? Total);
