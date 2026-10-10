using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Korkut.Telegram.Services;

/// <summary>The handful of Bot API calls Korkut makes. Every call names the bot by its token.</summary>
public interface ITelegramApi
{
    Task<TelegramUser> GetMeAsync(string token, CancellationToken ct = default);

    /// <summary>Long-polls for updates from <paramref name="offset"/> on, waiting up to <paramref name="timeoutSeconds"/>.</summary>
    Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(string token, long offset, int timeoutSeconds, CancellationToken ct = default);

    Task SendMessageAsync(string token, long chatId, string text, CancellationToken ct = default);

    Task SendTypingAsync(string token, long chatId, CancellationToken ct = default);
}

/// <summary>Telegram answered with <c>ok: false</c>, or not at all.</summary>
public sealed class TelegramApiException(int? errorCode, string description, int? retryAfter = null)
    : Exception(description)
{
    /// <summary>Telegram's HTTP-like code: 401 bad token, 409 someone else is polling this bot, 429 slow down.</summary>
    public int? ErrorCode { get; } = errorCode;

    public int? RetryAfter { get; } = retryAfter;
}

public sealed record TelegramUser(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("is_bot")] bool IsBot,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("username")] string? Username);

public sealed record TelegramChat(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("type")] string Type);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("from")] TelegramUser? From,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("text")] string? Text);

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message);

/// <summary>
/// <see cref="ITelegramApi"/> over HTTPS. The token is part of every URL, so this client's
/// requests are never logged (see TelegramModule) and errors never repeat the URL.
/// </summary>
internal sealed class TelegramApi(IHttpClientFactory factory) : ITelegramApi
{
    public const string HttpClientName = "telegram";

    private static readonly string[] MessagesOnly = ["message"];

    public Task<TelegramUser> GetMeAsync(string token, CancellationToken ct = default)
        => CallAsync<TelegramUser>(token, "getMe", new { }, ct);

    public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(string token, long offset, int timeoutSeconds, CancellationToken ct = default)
        => CallAsync<IReadOnlyList<TelegramUpdate>>(
            token, "getUpdates", new { offset, timeout = timeoutSeconds, allowed_updates = MessagesOnly }, ct);

    public Task SendMessageAsync(string token, long chatId, string text, CancellationToken ct = default)
        => CallAsync<JsonElement>(token, "sendMessage", new { chat_id = chatId, text }, ct);

    public Task SendTypingAsync(string token, long chatId, CancellationToken ct = default)
        => CallAsync<JsonElement>(token, "sendChatAction", new { chat_id = chatId, action = "typing" }, ct);

    private async Task<T> CallAsync<T>(string token, string method, object body, CancellationToken ct)
    {
        var client = factory.CreateClient(HttpClientName);
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync($"./bot{token}/{method}", body, ct);
        }
        catch (HttpRequestException)
        {
            throw new TelegramApiException(null, "Telegram'a ulaşılamadı.");
        }

        using (response)
        {
            Envelope<T>? envelope;
            try
            {
                envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(ct);
            }
            catch (JsonException)
            {
                envelope = null;
            }

            if (envelope is null)
            {
                throw new TelegramApiException((int)response.StatusCode, "Telegram anlaşılmayan bir yanıt verdi.");
            }

            if (!envelope.Ok || envelope.Result is null)
            {
                throw new TelegramApiException(
                    envelope.ErrorCode ?? (int)response.StatusCode, envelope.Description ?? "Telegram isteği reddetti.",
                    envelope.Parameters?.RetryAfter);
            }

            return envelope.Result;
        }
    }

    private sealed record Envelope<T>(
        [property: JsonPropertyName("ok")] bool Ok,
        [property: JsonPropertyName("result")] T? Result,
        [property: JsonPropertyName("error_code")] int? ErrorCode,
        [property: JsonPropertyName("description")] string? Description,
        [property: JsonPropertyName("parameters")] Parameters? Parameters);

    private sealed record Parameters([property: JsonPropertyName("retry_after")] int? RetryAfter);
}
