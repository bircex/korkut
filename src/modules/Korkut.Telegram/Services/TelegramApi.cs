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

    /// <summary>Sends a message; returns its id, so it can be edited.</summary>
    Task<long> SendMessageAsync(string token, long chatId, string text, CancellationToken ct = default);

    /// <summary>Replaces a sent message's text (how a reply grows while it is being written).</summary>
    Task EditMessageTextAsync(string token, long chatId, long messageId, string text, CancellationToken ct = default);

    /// <summary>Downloads a file a member sent, by its id. Telegram serves bots files up to 20 MB.</summary>
    Task<byte[]> DownloadFileAsync(string token, string fileId, CancellationToken ct = default);

    Task SendTypingAsync(string token, long chatId, CancellationToken ct = default);

    /// <summary>How many are in a group, the bot and any other bots included.</summary>
    Task<int> GetChatMemberCountAsync(string token, long chatId, CancellationToken ct = default);
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
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string? Title = null);

public sealed record TelegramMessage(
    [property: JsonPropertyName("message_id")] long MessageId,
    [property: JsonPropertyName("from")] TelegramUser? From,
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("caption")] string? Caption = null,
    [property: JsonPropertyName("document")] TelegramDocument? Document = null,
    [property: JsonPropertyName("photo")] IReadOnlyList<TelegramPhotoSize>? Photo = null,
    [property: JsonPropertyName("new_chat_members")] IReadOnlyList<TelegramUser>? NewChatMembers = null,
    [property: JsonPropertyName("left_chat_member")] TelegramUser? LeftChatMember = null,
    [property: JsonPropertyName("reply_to_message")] TelegramMessage? ReplyToMessage = null);

/// <summary>A file sent as a document (a PDF, a Word file).</summary>
public sealed record TelegramDocument(
    [property: JsonPropertyName("file_id")] string FileId,
    [property: JsonPropertyName("file_name")] string? FileName,
    [property: JsonPropertyName("mime_type")] string? MimeType,
    [property: JsonPropertyName("file_size")] long? FileSize);

/// <summary>One size of a sent photo; Telegram lists them smallest first.</summary>
public sealed record TelegramPhotoSize(
    [property: JsonPropertyName("file_id")] string FileId,
    [property: JsonPropertyName("width")] int Width,
    [property: JsonPropertyName("height")] int Height,
    [property: JsonPropertyName("file_size")] long? FileSize);

internal sealed record TelegramFile(
    [property: JsonPropertyName("file_id")] string FileId,
    [property: JsonPropertyName("file_path")] string? FilePath,
    [property: JsonPropertyName("file_size")] long? FileSize);

internal sealed record TelegramSentMessage([property: JsonPropertyName("message_id")] long MessageId);

public sealed record TelegramUpdate(
    [property: JsonPropertyName("update_id")] long UpdateId,
    [property: JsonPropertyName("message")] TelegramMessage? Message,
    [property: JsonPropertyName("chat_member")] TelegramChatMemberUpdated? ChatMember = null);

/// <summary>Someone joined, left or changed standing in a group (sent while the bot is an admin there).</summary>
public sealed record TelegramChatMemberUpdated(
    [property: JsonPropertyName("chat")] TelegramChat Chat,
    [property: JsonPropertyName("new_chat_member")] TelegramChatMember NewChatMember);

public sealed record TelegramChatMember(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("user")] TelegramUser User,
    [property: JsonPropertyName("is_member")] bool? IsMember = null)
{
    /// <summary>Whether this standing means being in the group.</summary>
    public bool InGroup => Status is "creator" or "administrator" or "member" || (Status == "restricted" && IsMember == true);
}

/// <summary>
/// <see cref="ITelegramApi"/> over HTTPS. The token is part of every URL, so this client's
/// requests are never logged (see TelegramModule) and errors never repeat the URL.
/// </summary>
internal sealed class TelegramApi(IHttpClientFactory factory) : ITelegramApi
{
    public const string HttpClientName = "telegram";

    // Messages, and membership changes in groups (Telegram sends those only to a group's admins).
    private static readonly string[] Updates = ["message", "chat_member"];

    public Task<TelegramUser> GetMeAsync(string token, CancellationToken ct = default)
        => CallAsync<TelegramUser>(token, "getMe", new { }, ct);

    public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(string token, long offset, int timeoutSeconds, CancellationToken ct = default)
        => CallAsync<IReadOnlyList<TelegramUpdate>>(
            token, "getUpdates", new { offset, timeout = timeoutSeconds, allowed_updates = Updates }, ct);

    /// <summary>The most a bot may download through the Bot API.</summary>
    public const long MaxDownloadBytes = 20 * 1024 * 1024;

    public async Task<long> SendMessageAsync(string token, long chatId, string text, CancellationToken ct = default)
        => (await CallAsync<TelegramSentMessage>(token, "sendMessage", new { chat_id = chatId, text }, ct)).MessageId;

    public Task EditMessageTextAsync(string token, long chatId, long messageId, string text, CancellationToken ct = default)
        => CallAsync<JsonElement>(token, "editMessageText", new { chat_id = chatId, message_id = messageId, text }, ct);

    public async Task<byte[]> DownloadFileAsync(string token, string fileId, CancellationToken ct = default)
    {
        var file = await CallAsync<TelegramFile>(token, "getFile", new { file_id = fileId }, ct);
        if (file.FilePath is null || file.FileSize > MaxDownloadBytes)
        {
            throw new TelegramApiException(400, "Dosya Telegram'dan indirilemeyecek kadar büyük.");
        }

        try
        {
            using var response = await factory.CreateClient(HttpClientName).GetAsync($"./file/bot{token}/{file.FilePath}", ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new TelegramApiException((int)response.StatusCode, "Dosya Telegram'dan indirilemedi.");
            }

            if (response.Content.Headers.ContentLength > MaxDownloadBytes)
            {
                throw new TelegramApiException(400, "Dosya Telegram'dan indirilemeyecek kadar büyük.");
            }

            return await response.Content.ReadAsByteArrayAsync(ct);
        }
        catch (HttpRequestException)
        {
            throw new TelegramApiException(null, "Telegram'a ulaşılamadı.");
        }
    }

    public Task SendTypingAsync(string token, long chatId, CancellationToken ct = default)
        => CallAsync<JsonElement>(token, "sendChatAction", new { chat_id = chatId, action = "typing" }, ct);

    public Task<int> GetChatMemberCountAsync(string token, long chatId, CancellationToken ct = default)
        => CallAsync<int>(token, "getChatMemberCount", new { chat_id = chatId }, ct);

    private async Task<T> CallAsync<T>(string token, string method, object body, CancellationToken ct)
    {
        var client = factory.CreateClient(HttpClientName);
        HttpResponseMessage response;
        try
        {
            // Buffered with a Content-Length rather than chunked: proxies in front of a local
            // install handle it more reliably.
            using var content = new StringContent(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            response = await client.PostAsync($"./bot{token}/{method}", content, ct);
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
