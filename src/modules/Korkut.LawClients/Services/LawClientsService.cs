using System.Text.Encodings.Web;
using System.Text.Json;
using Korkut.Contract.LawClients;
using Korkut.Core;
using Korkut.LawClients.Services.Shared;
using Korkut.LawClients.Services.Sources;
using Microsoft.Extensions.Caching.Memory;

namespace Korkut.LawClients.Services;

/// <summary>
/// <see cref="ILawClientsSearchService"/> + <see cref="ILawClientsDocumentService"/> over the same
/// <see cref="ISourceClient"/>s the agent's legal tools used to own directly (search + fetch +
/// the Mevzuat-specific rationale/article-tree/article-search reads). Dispatch by source name;
/// authorization is the caller's (controller policy or tool RequiredPermission) — these public
/// databases carry no tenant data.
/// </summary>
internal sealed class LawClientsService(
    IEnumerable<ISourceClient> clients,
    IEnvironment env,
    IMemoryCache cache) : ILawClientsSearchService, ILawClientsDocumentService
{
    // The drawer shows a whole decision at once, so page far larger than the model tool's
    // 5000-char default; `page` still allows walking truly huge documents.
    private const int DocumentPageSize = 20_000;

    // Short TTL: cuts repeated identical searches (many offices searching the same popular terms)
    // without risking stale results for a research tool where "a few minutes old" is harmless.
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(3);

    private static readonly JsonSerializerOptions ArgsJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<string, ISourceClient> _bySource =
        clients.ToDictionary(c => c.Source, StringComparer.OrdinalIgnoreCase);

    // Mevzuat/Sigorta Tahkim are also plain ISourceClients (dispatched by search/GetAsync like any
    // other source); these typed accessors are only for the few operations beyond that interface
    // (article tree, rationale, in-document search) that only those two sources support.
    private MevzuatClient Mevzuat => (MevzuatClient)_bySource["mevzuat"];

    private SigortaTahkimClient SigortaTahkimSource => (SigortaTahkimClient)_bySource["sigorta_tahkim"];

    // ---- ILawClientsSearchService ----

    public async Task<LawClientsSearchResponse> SearchAsync(string source, LawClientsSearchRequest request, CancellationToken ct = default)
    {
        var client = ResolveSearchable(source);

        var cacheKey = $"lawclients:search:{source.ToLowerInvariant()}:{JsonSerializer.Serialize(request, ArgsJson)}";
        if (cache.TryGetValue(cacheKey, out LawClientsSearchResponse? cached) && cached is not null)
        {
            return cached;
        }

        using var argsDoc = JsonSerializer.SerializeToDocument(request, ArgsJson);
        try
        {
            var outcome = await client.SearchPagedAsync(request.Query.Trim(), argsDoc.RootElement, ct);
            var response = new LawClientsSearchResponse(
                outcome.Total,
                outcome.Items.Count,
                outcome.Items.Select(ToContract).ToList());
            cache.Set(cacheKey, response, SearchCacheTtl);
            return response;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            throw new BadRequestException($"'{source}' kaynağında arama başarısız oldu: {ex.Message}");
        }
    }

    public async Task<LawClientsArticleSearchResponse> SearchWithinMevzuatAsync(
        string mevzuatId, string keyword, bool caseSensitive, int maxResults, CancellationToken ct = default)
    {
        string? text;
        try
        {
            text = await Mevzuat.GetDocumentTextAsync(mevzuatId.Trim(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException)
        {
            throw new BadRequestException($"Mevzuat metni getirilemedi: {ex.Message}");
        }
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new NotFoundException($"'{mevzuatId}' için mevzuat metni bulunamadı.");
        }

        var articles = MaddeSearch.Split(text);
        if (articles.Count == 0)
        {
            throw new BadRequestException(
                $"'{mevzuatId}' metni maddelere ayrılamadı (MADDE başlığı bulunamadı); tam metin için fetch_document kullanın.");
        }

        var matches = new List<(MaddeSearch.Madde Madde, int Score)>();
        foreach (var madde in articles)
        {
            var (isMatch, score) = MaddeSearch.Matches(madde.Content, keyword, caseSensitive);
            if (isMatch && score > 0)
            {
                matches.Add((madde, score));
            }
        }

        var ranked = matches
            .OrderByDescending(m => m.Score)
            .Take(maxResults)
            .Select(m => new LawClientsArticleMatch(
                m.Madde.No, m.Score, MaddeSearch.Preview(m.Madde.Content, keyword, caseSensitive), m.Madde.Content))
            .ToList();

        return new LawClientsArticleSearchResponse(mevzuatId, keyword, articles.Count, matches.Count, ranked);
    }

    public async Task<LawClientsInsuranceSearchResponse> SearchWithinSigortaTahkimAsync(
        string issueNumber, string keyword, int maxResults, CancellationToken ct = default)
    {
        var (text, _) = await SigortaTahkimSource.DownloadIssueTextAsync(issueNumber, ct);
        if (string.IsNullOrEmpty(text))
        {
            throw new NotFoundException($"Sayı {issueNumber} indirilemedi.");
        }

        var decisions = SigortaTahkim.SplitIntoDecisions(text);
        var keywordLower = SigortaTahkim.TurkishLower(keyword);

        const int excerptRadius = 200;
        var matches = new List<LawClientsInsuranceDecisionMatch>();
        foreach (var (header, body) in decisions)
        {
            var bodyLower = SigortaTahkim.TurkishLower(body);
            var count = CountOccurrences(bodyLower, keywordLower);
            if (count == 0)
            {
                continue;
            }
            var pos = bodyLower.IndexOf(keywordLower, StringComparison.Ordinal);
            var start = Math.Max(0, pos - excerptRadius);
            var end = Math.Min(body.Length, pos + keyword.Length + excerptRadius);
            var excerpt = body[start..end].Trim();
            if (start > 0)
            {
                excerpt = "..." + excerpt;
            }
            if (end < body.Length)
            {
                excerpt += "...";
            }
            matches.Add(new LawClientsInsuranceDecisionMatch(header, count, excerpt));
        }

        var ranked = matches.OrderByDescending(m => m.MatchCount).Take(maxResults).ToList();
        return new LawClientsInsuranceSearchResponse(issueNumber, keyword, decisions.Count, ranked.Count, ranked);
    }

    // ---- ILawClientsDocumentService ----

    public async Task<LawClientsDocumentResponse> GetAsync(
        string source, string id, string? articleId = null, int? page = null, CancellationToken ct = default)
    {
        if (!_bySource.TryGetValue(source, out var client) || !client.SupportsFetch
            || (client.RequiresKey is { } key && string.IsNullOrWhiteSpace(env.Get(key))))
        {
            throw new NotFoundException($"'{source}' kaynağından belge getirilemiyor.");
        }

        string? text;
        try
        {
            using var args = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                article_id = articleId,
                page = page is > 0 ? page : 1,
                page_size = DocumentPageSize,
            }));
            text = await client.FetchAsync(id.Trim(), args.RootElement.Clone(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException)
        {
            // Upstream's internal error text shouldn't leak to the user (may be English/technical);
            // the UI already shows an external-link fallback.
            throw new NotFoundException("Belge getirilemedi. Kaynak şu anda yanıt vermiyor olabilir.");
        }

        return string.IsNullOrWhiteSpace(text)
            ? throw new NotFoundException("Belge bulunamadı.")
            : new LawClientsDocumentResponse(client.Source, id.Trim(), text);
    }

    public async Task<LawClientsGerekceResponse> GetGerekceAsync(string gerekceId, int page = 1, int pageSize = 5000, CancellationToken ct = default)
    {
        (string? MevzuatId, string Text)? gerekce;
        try
        {
            gerekce = await Mevzuat.GetGerekceAsync(gerekceId.Trim(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException)
        {
            throw new BadRequestException($"Gerekçe getirilemedi: {ex.Message}");
        }
        if (gerekce is not { } found)
        {
            throw new NotFoundException($"'{gerekceId}' için yayımlanmış bir gerekçe bulunamadı.");
        }

        var clampedPageSize = Math.Clamp(pageSize, 500, 20000);
        var (chunk, actualPage, totalPages) = LegalText.ChunkMarkdown(found.Text, page, clampedPageSize);
        return new LawClientsGerekceResponse(found.MevzuatId, chunk, actualPage, totalPages);
    }

    public async Task<LawClientsMaddeTreeResponse> GetMaddeTreeAsync(string mevzuatId, CancellationToken ct = default)
    {
        JsonDocument? tree;
        try
        {
            tree = await Mevzuat.GetMaddeTreeAsync(mevzuatId.Trim(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            throw new BadRequestException($"Madde ağacı getirilemedi: {ex.Message}");
        }

        using (tree)
        {
            if (tree is null || !tree.RootElement.TryGetProperty("children", out var children)
                || children.ValueKind != JsonValueKind.Array)
            {
                return new LawClientsMaddeTreeResponse(0, []);
            }

            var count = 0;
            var nodes = children.EnumerateArray().Select(c => BuildNode(c, ref count)).ToList();
            return new LawClientsMaddeTreeResponse(count, nodes);
        }
    }

    // ---- helpers ----

    private ISourceClient ResolveSearchable(string source)
    {
        if (!_bySource.TryGetValue(source, out var client))
        {
            throw new BadRequestException($"Desteklenmeyen kaynak: {source}");
        }
        if (client.RequiresKey is { } key && string.IsNullOrWhiteSpace(env.Get(key)))
        {
            throw new BadRequestException($"'{source}' kaynağı için API anahtarı yapılandırılmamış.");
        }
        return client;
    }

    private static LawClientsSearchResult ToContract(SourceResult r) => new(
        r.Source, r.Id, r.Title, r.Date, r.CaseNumber, r.DecisionNumber, r.Court, r.Chamber, r.Url, r.Snippet,
        r.GazetteNumber, r.GerekceId, r.DecisionType, r.FinalizationStatus, r.CourtType);

    // Kitap/kısım/bölüm headings also carry a maddeId, so the leaf test is the article number.
    private static LawClientsMaddeTreeNode BuildNode(JsonElement node, ref int count)
    {
        var maddeNo = Str(node, "maddeNo");
        var title = maddeNo is not null
            ? Str(node, "title") ?? Str(node, "maddeBaslik") ?? "(başlıksız)"
            : Str(node, "title") ?? Str(node, "description") ?? Str(node, "maddeBaslik");

        if (title is not null || maddeNo is not null)
        {
            count++;
        }

        var children = new List<LawClientsMaddeTreeNode>();
        if (node.TryGetProperty("children", out var kids) && kids.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in kids.EnumerateArray())
            {
                children.Add(BuildNode(child, ref count));
            }
        }

        return new LawClientsMaddeTreeNode(title, maddeNo, Str(node, "maddeId"), Str(node, "gerekceId"), children);
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() :
        e.TryGetProperty(name, out var n) && n.ValueKind is JsonValueKind.Number ? n.ToString() : null;

    private static int CountOccurrences(string haystack, string needle)
    {
        if (needle.Length == 0)
        {
            return 0;
        }
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
