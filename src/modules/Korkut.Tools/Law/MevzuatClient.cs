using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Tools.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// Turkish legislation (mevzuat) search over the Ministry of Justice UYAP Mevzuat Bedesten
/// endpoint (<c>/mevzuat/searchDocuments</c>): laws, decree-laws, statutes, regulations,
/// Presidential decrees/decisions/circulars, communiqués. Public; no API key. Ported from the
/// Python <c>search_mevzuat</c> (bedesten variant): title/content/full-text scopes, exact-phrase,
/// legislation-number and Official Gazette filters. Also serves the madde-tree, gerekçe and
/// plain-text endpoints the article-level tools build on.
/// </summary>
public sealed class MevzuatClient(IHttpClientFactory httpClientFactory) : ISourceClient
{
    private const string SearchPath = "/mevzuat/searchDocuments";

    private const string DocumentPath = "/mevzuat/getDocumentContent";

    private const string MaddeTreePath = "/mevzuat/mevzuatMaddeTree";

    private const string GerekcePath = "/mevzuat/getGerekceContent";

    public string Source => "mevzuat";

    public string? RequiresKey => null;

    public bool SupportsFetch => true;

    public async Task<IReadOnlyList<SourceResult>> SearchAsync(string query, JsonElement args, CancellationToken ct = default)
    {
        var limit = Math.Clamp(ToolArgs.Int(args, "limit") ?? 10, 1, 50);
        var page = Math.Max(1, ToolArgs.Int(args, "page") ?? 1);

        var data = new Dictionary<string, object?>
        {
            ["pageSize"] = limit,
            ["pageNumber"] = page,
            ["sortFields"] = new[] { "RESMI_GAZETE_TARIHI" },
            ["sortDirection"] = "desc",
        };

        var mevzuatNo = ToolArgs.String(args, "mevzuat_no");
        // Kanun numbers never match the title index ("5237" finds nothing); route digit-only
        // queries to the number filter the API does support.
        if (string.IsNullOrWhiteSpace(mevzuatNo) && query.Trim() is { Length: > 0 } q && q.All(char.IsAsciiDigit))
        {
            mevzuatNo = q;
            query = string.Empty;
        }
        if (!string.IsNullOrWhiteSpace(mevzuatNo))
        {
            data["mevzuatNo"] = mevzuatNo.Trim();
        }

        var scope = ToolArgs.String(args, "search_scope")?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(query))
        {
            switch (scope)
            {
                case "content":
                    // Full-text search inside the legislation body (Solr syntax passes through:
                    // "tam ifade", +zorunlu -hariç, joker*, bulanık~).
                    data["phrase"] = query;
                    break;
                case "title_and_content":
                    data["mevzuatAdi"] = query;
                    data["basliktaAra"] = false;
                    break;
                default:
                    data["mevzuatAdi"] = query;
                    break;
            }
            if (ToolArgs.Bool(args, "tam_cumle"))
            {
                data["tamCumle"] = true;
            }
        }

        var types = (ToolArgs.String(args, "legislation_type") ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToUpperInvariant())
            .ToArray();

        if (types.Length > 0)
        {
            data["mevzuatTurList"] = types;
        }

        // date_start/date_end filter on the Official Gazette date here (decision sources use them
        // for the ruling date). Turkey is UTC+3, so local midnight is the previous day 21:00 UTC;
        // the end bound uses the end date's own 21:00 UTC to keep that whole day in range.
        if (ToolArgs.Date(args, "date_start") is { } start)
        {
            data["resmiGazeteTarihiStart"] = $"{start.AddDays(-1):yyyy-MM-dd}T21:00:00.000Z";
        }
        if (ToolArgs.Date(args, "date_end") is { } end)
        {
            data["resmiGazeteTarihiEnd"] = $"{end:yyyy-MM-dd}T21:00:00.000Z";
        }
        if (ToolArgs.String(args, "gazette_number") is { Length: > 0 } gazette)
        {
            data["resmiGazeteSayisi"] = gazette.Trim();
        }

        using var body = await PostJsonAsync(SearchPath,
            new { data, applicationName = "UyapMevzuat", paging = true }, ct);

        if (!body.RootElement.TryGetProperty("data", out var payload) ||
            !payload.TryGetProperty("mevzuatList", out var list) ||
            list.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<SourceResult>(list.GetArrayLength());

        foreach (var e in list.EnumerateArray())
        {
            var id = Str(e, "mevzuatId");

            if (id is null)
            {
                continue;
            }

            results.Add(new SourceResult(
                Source: "mevzuat",
                Id: id,
                Title: Str(e, "mevzuatAdi") ?? id,
                Date: Str(e, "resmiGazeteTarihi"),
                DecisionNumber: Str(e, "mevzuatNo"),
                Court: LegislationType(e),
                Url: $"https://mevzuat.adalet.gov.tr/mevzuat?mevzuatId={id}",
                GazetteNumber: Str(e, "resmiGazeteSayisi"),
                GerekceId: Str(e, "gerekceId")));
        }
        return results;
    }

    public async Task<string?> FetchAsync(string id, JsonElement args, CancellationToken ct = default)
    {
        // A whole piece of legislation (MEVZUAT) or, when article_id is given, a single article (MADDE).
        var articleId = ToolArgs.String(args, "article_id");
        var (documentId, documentType) = string.IsNullOrWhiteSpace(articleId)
            ? (id, "MEVZUAT")
            : (articleId!, "MADDE");

        var (title, markdown) = await FetchContentAsync(documentType, documentId, ct);
        if (markdown is null)
        {
            return null;
        }

        var sourceUrl = $"https://mevzuat.adalet.gov.tr/mevzuat?mevzuatId={id}";
        var header = string.IsNullOrEmpty(title) ? $"Kaynak: {sourceUrl}" : $"{title}\nKaynak: {sourceUrl}";
        return $"{header}\n\n{markdown}";
    }

    /// <summary>Full text of one piece of legislation with no source header, for the article-level
    /// search tool to split into MADDE blocks. Null when the document has no content.</summary>
    public async Task<string?> GetDocumentTextAsync(string mevzuatId, CancellationToken ct = default)
    {
        var (_, markdown) = await FetchContentAsync("MEVZUAT", mevzuatId, ct);
        return markdown;
    }

    /// <summary>
    /// The madde tree (içindekiler) of one piece of legislation: nested kısım/bölüm nodes with
    /// MADDE leaves carrying <c>maddeId</c> (usable as <c>fetch_document article_id</c>) and
    /// per-article <c>gerekceId</c>. Returns the parsed <c>data</c> element; caller disposes.
    /// </summary>
    public async Task<JsonDocument?> GetMaddeTreeAsync(string mevzuatId, CancellationToken ct = default)
    {
        var body = await PostJsonAsync(MaddeTreePath,
            new { data = new { mevzuatId }, applicationName = "UyapMevzuat" }, ct);
        if (!body.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            body.Dispose();
            return null;
        }
        try
        {
            return JsonDocument.Parse(data.GetRawText());
        }
        finally
        {
            body.Dispose();
        }
    }

    /// <summary>
    /// Law rationale (gerekçe) by gerekceId — the law-level id from search results or an
    /// article-level id from the madde tree. Returns the owning mevzuatId and the rationale as
    /// readable text, or null when nothing is published under that id.
    /// </summary>
    public async Task<(string? MevzuatId, string Text)?> GetGerekceAsync(string gerekceId, CancellationToken ct = default)
    {
        using var body = await PostJsonAsync(GerekcePath,
            new { data = new { gerekceId }, applicationName = "UyapMevzuat" }, ct);

        if (!body.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        var contentB64 = Str(data, "content");
        if (string.IsNullOrEmpty(contentB64))
        {
            return null;
        }

        var mime = Str(data, "mimeType") ?? Str(data, "mimetype") ?? "text/html";
        var text = DecodeContent(contentB64, mime);
        return string.IsNullOrWhiteSpace(text) ? null : (Str(data, "mevzuatId"), text);
    }

    private async Task<(string? Title, string? Markdown)> FetchContentAsync(
        string documentType, string documentId, CancellationToken ct)
    {
        using var body = await PostJsonAsync(DocumentPath,
            new { data = new { documentType, id = documentId }, applicationName = "UyapMevzuat" }, ct);

        if (!body.RootElement.TryGetProperty("data", out var data))
        {
            return (null, null);
        }
        var contentB64 = Str(data, "content");
        if (string.IsNullOrEmpty(contentB64))
        {
            return (null, null);
        }

        var mime = Str(data, "mimeType") ?? Str(data, "mimetype") ?? "text/html";
        var title = Str(data, "mevzuatAdi") ?? Str(data, "maddeBaslik") ?? Str(data, "baslik");
        return (title, DecodeContent(contentB64, mime));
    }

    private static string DecodeContent(string contentB64, string mime)
    {
        var raw = Convert.FromBase64String(contentB64);
        return mime.Contains("pdf", StringComparison.OrdinalIgnoreCase)
            ? LegalText.PdfToText(raw)
            : mime.Contains("html", StringComparison.OrdinalIgnoreCase)
                ? LegalText.HtmlToMarkdown(System.Text.Encoding.UTF8.GetString(raw))
                : System.Text.Encoding.UTF8.GetString(raw);
    }

    /// <summary>POST + parse, surfacing the API's own failure envelope (FMTY/FMTE) as an error.</summary>
    private async Task<JsonDocument> PostJsonAsync(string path, object payload, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("bedesten");
        using var resp = await client.PostAsJsonAsync(path, payload, ct);
        resp.EnsureSuccessStatusCode();
        var stream = await resp.Content.ReadAsStreamAsync(ct);
        var body = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (body.RootElement.TryGetProperty("metadata", out var meta) &&
            Str(meta, "FMTY") is { } fmty && fmty != "SUCCESS")
        {
            var message = Str(meta, "FMTE") ?? "Bedesten API hatası";
            body.Dispose();
            throw new InvalidOperationException(message);
        }
        return body;
    }

    private static string? LegislationType(JsonElement entry)
    {
        if (!entry.TryGetProperty("mevzuatTur", out var tur))
        {
            return null;
        }

        return tur.ValueKind switch
        {
            JsonValueKind.String => tur.GetString(),
            JsonValueKind.Object => Str(tur, "name") ?? Str(tur, "description"),
            _ => null,
        };
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String ? v.GetString() :
        e.TryGetProperty(name, out var n) && n.ValueKind is JsonValueKind.Number ? n.ToString() : null;
}
