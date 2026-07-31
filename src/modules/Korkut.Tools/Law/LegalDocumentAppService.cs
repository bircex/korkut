using System.Text.Json;
using Korkut.Contract.Agent.Legal;
using Korkut.Core;

namespace Korkut.Tools.Law;

/// <summary>
/// <see cref="ILegalDocumentAppService"/> over the same <see cref="ISourceClient"/>s the
/// fetch/search tools use: dispatch by source, fetch the full text by id. No tenant data is
/// involved (public databases); authorization is the controller's documents.read policy.
/// </summary>
public sealed class LegalDocumentAppService(IEnumerable<ISourceClient> clients, IEnvironment env) : ILegalDocumentAppService
{
    // The drawer shows a whole decision at once, so page far larger than the model tool's
    // 5000-char default; `page` still allows walking truly huge documents.
    private const int PageSize = 20_000;

    private readonly Dictionary<string, ISourceClient> _bySource =
        clients.ToDictionary(c => c.Source, StringComparer.OrdinalIgnoreCase);

    public async Task<LegalDocumentResponse> GetAsync(string source, string id, int? page = null, CancellationToken ct = default)
    {
        if (!_bySource.TryGetValue(source, out var client) || !client.SupportsFetch
            || (client.RequiresKey is { } key && string.IsNullOrWhiteSpace(env.Get(key))))
        {
            throw new NotFoundException($"'{source}' kaynağından belge getirilemiyor.");
        }

        string? text;
        try
        {
            using var args = JsonDocument.Parse(
                JsonSerializer.Serialize(new { page = page is > 0 ? page : 1, page_size = PageSize }));
            text = await client.FetchAsync(id.Trim(), args.RootElement.Clone(), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException or FormatException)
        {
            // Upstream'in iç hata metni kullanıcıya sızmasın (İngilizce/teknik olabilir); UI zaten
            // dış bağlantı fallback'i gösteriyor.
            throw new NotFoundException("Belge getirilemedi. Kaynak şu anda yanıt vermiyor olabilir.");
        }

        return string.IsNullOrWhiteSpace(text)
            ? throw new NotFoundException("Belge bulunamadı.")
            : new LegalDocumentResponse(client.Source, id.Trim(), text);
    }
}
