namespace Korkut.Telegram.Services;

/// <summary>
/// What the poller last ran into, for the owner's settings screen: a rejected token, another
/// program reading the bot, Telegram out of reach. Null while messages are coming in. Kept in
/// memory; it describes this running instance.
/// </summary>
public sealed class TelegramBotStatus
{
    private volatile string? _problem;

    public string? Problem => _problem;

    public void Report(string? problem) => _problem = problem;
}
