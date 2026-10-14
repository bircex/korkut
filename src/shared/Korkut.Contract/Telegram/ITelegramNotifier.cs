namespace Korkut.Contract.Telegram;

/// <summary>
/// Sends a notice to members through the office bot. Members without a linked chat, or an office
/// whose bot is off, simply receive nothing. Opens its own tenant scope for the reads; call it
/// outside one.
/// </summary>
public interface ITelegramNotifier
{
    /// <summary>Sends <paramref name="text"/> to the given members (all linked members when null);
    /// returns how many chats it reached.</summary>
    Task<int> NotifyAsync(Guid officeId, IReadOnlyCollection<Guid>? userIds, string text, CancellationToken ct = default);
}
