using System.ComponentModel.DataAnnotations;

namespace Korkut.Contract.Telegram;

/// <summary>A member whose Telegram chat is linked to their Korkut account.</summary>
public sealed record TelegramLinkedMember(Guid UserId, string Name, string? TelegramUsername, DateTimeOffset LinkedAt);

/// <summary>
/// The office's Telegram bot as the owner sees it. The token itself is never returned; the bot's
/// username shows which bot is connected. <c>Problem</c> is what keeps the bot from receiving
/// messages right now, if anything.
/// </summary>
public sealed record TelegramSettingsResponse(
    bool Configured, bool Enabled, string? BotUsername, DateTimeOffset? ConsentAt, IReadOnlyList<TelegramLinkedMember> Members,
    string? Problem = null);

/// <summary>
/// Body for changing the office's bot. A token, when given, replaces the stored one (and unlinks
/// every member, since the chats belong to the old bot). Turning the bot on needs a token and the
/// owner's consent that messages pass through Telegram's servers.
/// </summary>
public sealed record SaveTelegramSettingsRequest
{
    [MaxLength(200, ErrorMessage = "Bot anahtarı en fazla 200 karakter olabilir.")]
    public string? Token { get; init; }

    public bool Enabled { get; init; }

    public bool Consent { get; init; }
}

/// <summary>The caller's own Telegram link, and whether the office's bot is there to link to.</summary>
public sealed record TelegramLinkResponse(bool Available, string? BotUsername, bool Linked, string? TelegramUsername, DateTimeOffset? LinkedAt);

/// <summary>A one-time code that links the caller's Telegram chat, and the bot link that sends it.</summary>
public sealed record TelegramLinkCodeResponse(string Code, string Url, DateTimeOffset ExpiresAt);

/// <summary>The office owner's bot settings (office.settings).</summary>
public interface ITelegramSettingsService
{
    Task<Result<TelegramSettingsResponse>> GetAsync(CancellationToken ct = default);

    Task<Result<TelegramSettingsResponse>> SaveAsync(SaveTelegramSettingsRequest request, CancellationToken ct = default);

    /// <summary>Forgets the bot and every member's link.</summary>
    Task<Result<TelegramSettingsResponse>> RemoveAsync(CancellationToken ct = default);

    Task<Result<TelegramSettingsResponse>> UnlinkMemberAsync(Guid userId, CancellationToken ct = default);
}

/// <summary>Each member's own link between their Korkut account and their Telegram chat.</summary>
public interface ITelegramLinkService
{
    Task<Result<TelegramLinkResponse>> GetAsync(CancellationToken ct = default);

    Task<Result<TelegramLinkCodeResponse>> CreateCodeAsync(CancellationToken ct = default);

    Task<Result<TelegramLinkResponse>> UnlinkAsync(CancellationToken ct = default);
}
