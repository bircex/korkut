using System.Net;
using System.Net.Http;
using System.Text;
using Korkut.Contract.Documents.OCR;
using Korkut.Documents.Services;
using Microsoft.AspNetCore.Http;

namespace Korkut.Documents.Tests;

/// <summary>
/// Pins the wire contract between a calling host and the internal Korkut.Ocr host: the multipart
/// field name, the snake_case response shape (<c>page_texts</c>), the forwarded caller auth that
/// gets past <c>matters.read</c>, and the single <see cref="OcrUnavailableException"/> failure shape
/// every error collapses into (including the 503 an unconfigured engine returns).
/// </summary>
public class HttpOcrServiceTests
{
    /// <summary>Captures the outgoing request and replays a canned response.</summary>
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? SentFieldName { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            // Read the multipart body here: the content is disposed once the caller's using-block ends.
            var raw = await request.Content!.ReadAsStringAsync(ct);
            SentFieldName = raw.Contains("name=file", StringComparison.Ordinal) ? "file" : null;

            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static (HttpOcrService Sut, StubHandler Handler) Build(
        HttpStatusCode status, string body, string? authorization = null)
    {
        var handler = new StubHandler(status, body);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        if (authorization is not null)
        {
            accessor.HttpContext!.Request.Headers.Authorization = authorization;
        }

        return (new HttpOcrService(new StubFactory(handler), accessor, "http://ocr:8080/"), handler);
    }

    private static MemoryStream Pdf() => new(Encoding.UTF8.GetBytes("%PDF-1.4 taranmis"));

    [Fact]
    public async Task ExtractAsync_ParsesTheSnakeCaseResponseIncludingPerPageTexts()
    {
        var (sut, handler) = Build(
            HttpStatusCode.OK,
            """{"text":"Sayfa bir\n\nSayfa iki","pages":2,"page_texts":["Sayfa bir","Sayfa iki"]}""");

        var result = await sut.ExtractAsync(Pdf(), "application/pdf");

        Assert.Equal(2, result.Pages);
        Assert.Equal(["Sayfa bir", "Sayfa iki"], result.PageTextsOrWhole);
        Assert.Equal("file", handler.SentFieldName);
        Assert.Equal("http://ocr:8080/extract", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task ExtractAsync_ForwardsTheCallersAuthorization()
    {
        // The endpoint is guarded by matters.read; without the caller's own token it is a 401.
        var (sut, handler) = Build(
            HttpStatusCode.OK, """{"text":"x","pages":1,"page_texts":["x"]}""", "Bearer token-123");

        await sut.ExtractAsync(Pdf(), "application/pdf");

        Assert.Equal("Bearer token-123", handler.Request!.Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task ExtractAsync_WithAMalformedContentType_StillSendsTheRequest()
    {
        // The declared type is caller-supplied; a bad header must not break the OCR call.
        var (sut, _) = Build(HttpStatusCode.OK, """{"text":"x","pages":1,"page_texts":["x"]}""");

        var result = await sut.ExtractAsync(Pdf(), "not a content type");

        Assert.Equal(1, result.Pages);
    }

    [Fact]
    public async Task ExtractAsync_WhenTheEngineIsUnconfigured_ReportsOcrUnavailable()
    {
        // 503 is exactly what the host returns while the DisabledOcrService scaffold is in place.
        var (sut, _) = Build(HttpStatusCode.ServiceUnavailable, """{"detail":"OCR motoru aktif değil!"}""");

        var ex = await Assert.ThrowsAsync<OcrUnavailableException>(() => sut.ExtractAsync(Pdf(), "application/pdf"));
        Assert.Contains("503", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_WhenTheHostIsUnreachable_ReportsOcrUnavailable()
    {
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var sut = new HttpOcrService(new StubFactory(new ThrowingHandler()), accessor, "http://ocr:8080");

        await Assert.ThrowsAsync<OcrUnavailableException>(() => sut.ExtractAsync(Pdf(), "application/pdf"));
    }

    [Fact]
    public async Task ExtractAsync_LeavesTheCallersStreamOpen()
    {
        // HttpContent disposes its parts, and StreamContent would close the wrapped stream with it —
        // but the stream belongs to the caller (the extractor still owns its buffer).
        var (sut, _) = Build(HttpStatusCode.OK, """{"text":"x","pages":1,"page_texts":["x"]}""");
        var stream = Pdf();

        await sut.ExtractAsync(stream, "application/pdf");

        Assert.True(stream.CanRead);
        stream.Position = 0;
    }

    [Fact]
    public async Task ExtractAsync_OnItsOwnTimeout_ReportsOcrUnavailableRatherThanCancellation()
    {
        // HttpClient signals its timeout as a cancellation. If that escaped as-is the caller's
        // graceful degradation would not catch it and the whole upload would fail.
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var sut = new HttpOcrService(new StubFactory(new TimingOutHandler()), accessor, "http://ocr:8080");

        var ex = await Assert.ThrowsAsync<OcrUnavailableException>(
            () => sut.ExtractAsync(Pdf(), "application/pdf", CancellationToken.None));
        Assert.Contains("zaman aşımına", ex.Message);
    }

    [Fact]
    public async Task ExtractAsync_WhenTheCallerCancels_PropagatesTheCancellation()
    {
        // A caller giving up is not an OCR failure and must not be reported as one.
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        var sut = new HttpOcrService(new StubFactory(new TimingOutHandler()), accessor, "http://ocr:8080");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.ExtractAsync(Pdf(), "application/pdf", cancelled.Token));
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new HttpRequestException("connection refused");
    }

    /// <summary>Fails the way HttpClient's own timeout does: a cancellation the caller did not ask for.</summary>
    private sealed class TimingOutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");
    }
}
