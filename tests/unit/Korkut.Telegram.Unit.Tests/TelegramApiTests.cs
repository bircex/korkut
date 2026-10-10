using System.Net;
using System.Text;
using Korkut.Telegram.Services;

namespace Korkut.Telegram.Unit.Tests;

/// <summary>The Bot API client against a stand-in Telegram that answers with canned envelopes.</summary>
public class TelegramApiTests
{
    private sealed class Stub(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("https://telegram.test/") };
    }

    [Fact]
    public async Task Updates_are_read_with_the_token_in_the_path()
    {
        var stub = new Stub(HttpStatusCode.OK, """
            {"ok":true,"result":[{"update_id":7,"message":{"message_id":1,"from":{"id":5,"is_bot":false,"first_name":"Elif","username":"elif"},"chat":{"id":5,"type":"private"},"text":"merhaba"}}]}
            """);

        var updates = await new TelegramApi(new Factory(stub)).GetUpdatesAsync("123:abc", 7, 25);

        var update = Assert.Single(updates);
        Assert.Equal(7, update.UpdateId);
        Assert.Equal("merhaba", update.Message!.Text);
        Assert.Equal("elif", update.Message.From!.Username);
        Assert.Equal("/bot123:abc/getUpdates", Assert.Single(stub.Paths));
    }

    [Fact]
    public async Task A_refusal_carries_Telegrams_code_and_wait()
    {
        var stub = new Stub(HttpStatusCode.TooManyRequests, """
            {"ok":false,"error_code":429,"description":"Too Many Requests: retry after 9","parameters":{"retry_after":9}}
            """);

        var ex = await Assert.ThrowsAsync<TelegramApiException>(() => new TelegramApi(new Factory(stub)).SendMessageAsync("t", 1, "x"));

        Assert.Equal(429, ex.ErrorCode);
        Assert.Equal(9, ex.RetryAfter);
    }

    [Fact]
    public async Task A_page_that_is_not_the_Bot_API_is_an_error_not_a_crash()
    {
        var stub = new Stub(HttpStatusCode.BadGateway, "<html>bad gateway</html>");

        var ex = await Assert.ThrowsAsync<TelegramApiException>(() => new TelegramApi(new Factory(stub)).GetMeAsync("t"));

        Assert.Equal(502, ex.ErrorCode);
        Assert.DoesNotContain("bot", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
