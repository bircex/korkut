using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Korkut.Contract.Documents.OCR;
using Microsoft.AspNetCore.Http;

namespace Korkut.Documents.Services;

/// <summary>
/// The adapter that reaches the internal Korkut.Ocr host (<c>KORKUT_OCR_URL</c> + <c>/extract</c>)
/// from a host that has no native OCR binaries of its own: recognition is heavy and
/// native-dependency-bound, so it stays in its own image and every other host calls it over HTTP.
/// The caller's own auth is forwarded (the endpoint is guarded by <c>matters.read</c>), matching how
/// <c>HttpKnowledgeSearch</c> reaches Knowledge. Any non-success response becomes
/// <see cref="OcrUnavailableException"/> — including the 503 the host returns when no engine is
/// configured — so a caller sees one failure shape regardless of where the OCR path broke.
/// </summary>
internal sealed class HttpOcrService(
    IHttpClientFactory httpFactory, IHttpContextAccessor accessor, string baseUrl) : IOcrService
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    private readonly string _baseUrl = baseUrl.TrimEnd('/');

    /// <summary>True whenever a URL is configured; whether the far side has a real engine shows up as a 503 on use.</summary>
    public bool IsAvailable => true;

    public async Task<OcrServiceResult> ExtractAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        // Disposing the form disposes its parts, and StreamContent disposes the stream it wraps —
        // which is the CALLER's stream here, not ours to close. The shim forwards reads and swallows
        // the dispose so the caller keeps a usable stream after OCR.
        var file = new StreamContent(new NonDisposingStream(content));
        // The declared content-type is caller-supplied and may be malformed; a bad header must not
        // fail the OCR call, and the engine sniffs the bytes itself anyway.
        file.Headers.ContentType = MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            ? parsed
            : new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "upload");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/extract") { Content = form };
        ForwardCallerAuth(request);

        HttpResponseMessage response;
        try
        {
            response = await httpFactory.CreateClient(OcrHttpClient.Name).SendAsync(request, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // The client's own timeout, not the caller giving up: a stalled OCR host must surface as
            // "OCR unavailable" (which callers degrade on) rather than as a cancelled request.
            throw new OcrUnavailableException("OCR servisi zaman aşımına uğradı.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new OcrUnavailableException($"OCR servisine ulaşılamadı: {ex.Message}");
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new OcrUnavailableException($"OCR servisi {(int)response.StatusCode} döndü.");
            }

            var body = await response.Content.ReadFromJsonAsync<OcrResponseDto>(Json, ct)
                ?? throw new OcrUnavailableException("OCR servisi boş yanıt döndü.");

            return new OcrServiceResult(body.Text ?? string.Empty, body.Pages, body.PageTexts);
        }
    }

    // The OCR host authenticates the request itself (matters.read) — pass the caller's session
    // through rather than minting a service identity, so OCR is only ever done for a real caller
    // who is already allowed to read the document.
    private void ForwardCallerAuth(HttpRequestMessage request)
    {
        var incoming = accessor.HttpContext?.Request;
        if (incoming is null)
        {
            return;
        }

        if (incoming.Headers.TryGetValue("Cookie", out var cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie.ToArray());
        }

        if (incoming.Headers.TryGetValue("Authorization", out var auth))
        {
            request.Headers.TryAddWithoutValidation("Authorization", auth.ToArray());
        }
    }

    private sealed record OcrResponseDto(string? Text, int Pages, List<string>? PageTexts);

    /// <summary>
    /// Read-through view over a stream this class does not own, so <see cref="HttpContent"/>'s
    /// disposal of its parts cannot close the caller's stream.
    /// </summary>
    private sealed class NonDisposingStream(Stream inner) : Stream
    {
        public override bool CanRead => inner.CanRead;

        public override bool CanSeek => inner.CanSeek;

        public override bool CanWrite => false;

        public override long Length => inner.Length;

        public override long Position
        {
            get => inner.Position;
            set => inner.Position = value;
        }

        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default) =>
            inner.ReadAsync(buffer, ct);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            inner.ReadAsync(buffer, offset, count, ct);

        public override long Seek(long offset, SeekOrigin origin) => inner.Seek(offset, origin);

        public override void Flush()
        {
        }

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            // Deliberately does not touch `inner` — that is the point of this shim. Stream's own
            // Dispose(bool) is a no-op, but call it so the base contract is honoured (CA2215).
            base.Dispose(disposing);
        }
    }
}

/// <summary>The named HttpClient the OCR adapter uses (its own, so the OCR timeout is not the AI timeout).</summary>
internal static class OcrHttpClient
{
    public const string Name = "ocr";

    /// <summary>
    /// A multi-page scan legitimately runs for minutes (rasterise + recognise every page), well past
    /// HttpClient's 100s default, so the ceiling is generous. No retry handler: a retried OCR call
    /// pays the whole cost again and the caller already degrades gracefully on failure.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);
}
