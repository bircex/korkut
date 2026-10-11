using Korkut.Core;

namespace Korkut.Telegram.Entities;

/// <summary>
/// The office's Telegram bot: its token (sealed with the install's secret key), whether it is on,
/// and the owner's consent that messages pass through Telegram's servers. One row per office.
/// </summary>
public sealed class TelegramBot : ITenantOwned, IAggregateRoot
{
    public TelegramBot(Guid officeId) => OfficeId = officeId;

    /// <summary>Materialization constructor for the ORM.</summary>
    private TelegramBot()
    {
    }

    public Guid OfficeId { get; private set; }

    /// <summary>The bot token, encrypted; null when no bot is connected.</summary>
    public string? TokenCipher { get; private set; }

    /// <summary>The bot's @username, as Telegram reported it when the token was checked.</summary>
    public string? BotUsername { get; private set; }

    public bool Enabled { get; private set; }

    public DateTimeOffset? ConsentAt { get; private set; }

    public Guid? ConsentBy { get; private set; }

    /// <summary>The next Telegram update to ask for, so a restart neither repeats nor skips messages.</summary>
    public long UpdateOffset { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Connect(string tokenCipher, string botUsername, DateTimeOffset now)
    {
        // Update ids belong to a bot; a new token for the same bot keeps the place in its queue.
        if (!string.Equals(BotUsername, botUsername, StringComparison.OrdinalIgnoreCase))
        {
            UpdateOffset = 0;
        }

        TokenCipher = tokenCipher;
        BotUsername = botUsername;
        UpdatedAt = now;
    }

    public void Configure(bool enabled, Guid by, bool consent, DateTimeOffset now)
    {
        if (consent && ConsentAt is null)
        {
            ConsentAt = now;
            ConsentBy = by;
        }

        Enabled = enabled;
        UpdatedAt = now;
    }

    public void Advance(long nextOffset) => UpdateOffset = Math.Max(UpdateOffset, nextOffset);
}
