using System.Diagnostics;
using System.Text;

namespace Korkut.Telegram.Services;

/// <summary>
/// Shows a reply in Telegram while it is being written. The first words open a message, which is
/// edited as more arrive, at most once per <see cref="EditInterval"/> (Telegram throttles bots that
/// edit faster); the finished reply then replaces it, continuing in further messages when it is
/// longer than one message allows. Showing the reply early is a courtesy: if an edit fails, the
/// finished reply still goes out.
/// </summary>
internal sealed class StreamingReply(ITelegramApi telegram, string token, long chatId, TimeSpan? editInterval = null)
{
    public static readonly TimeSpan DefaultEditInterval = TimeSpan.FromSeconds(1.5);

    private readonly StringBuilder _text = new();
    private readonly TimeSpan _interval = editInterval ?? DefaultEditInterval;
    private long? _messageId;
    private string _shown = string.Empty;
    private long _lastEdit;

    public TimeSpan EditInterval => _interval;

    /// <summary>Whether a message is already on screen.</summary>
    public bool Started => _messageId is not null;

    /// <summary>What has been written so far.</summary>
    public string Text => _text.ToString();

    public async Task AppendAsync(string delta, CancellationToken ct = default)
    {
        _text.Append(delta);
        var preview = Preview(_text.ToString());
        if (preview.Length == 0 || preview == _shown)
        {
            return;
        }

        try
        {
            if (_messageId is null)
            {
                _messageId = await telegram.SendMessageAsync(token, chatId, preview, ct);
            }
            else if (Stopwatch.GetElapsedTime(_lastEdit) >= _interval)
            {
                await telegram.EditMessageTextAsync(token, chatId, _messageId.Value, preview, ct);
            }
            else
            {
                return;
            }

            _shown = preview;
            _lastEdit = Stopwatch.GetTimestamp();
        }
        catch (TelegramApiException)
        {
            // Too fast, or Telegram busy: the next delta or the finished reply tries again.
        }
    }

    /// <summary>Puts the finished reply on screen: the open message becomes its first part.</summary>
    public async Task FinishAsync(string reply, CancellationToken ct = default)
    {
        var parts = TelegramText.Split(reply);
        var rest = parts.AsEnumerable();
        if (_messageId is { } id && parts.Count > 0)
        {
            rest = parts.Skip(1);
            if (parts[0] != _shown)
            {
                try
                {
                    await telegram.EditMessageTextAsync(token, chatId, id, parts[0], ct);
                }
                catch (TelegramApiException)
                {
                    await telegram.SendMessageAsync(token, chatId, parts[0], ct);
                }
            }
        }

        foreach (var part in rest)
        {
            await telegram.SendMessageAsync(token, chatId, part, ct);
        }
    }

    /// <summary>The text so far, cut to fit one message while it is still growing.</summary>
    private static string Preview(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Length <= TelegramText.MaxLength ? trimmed : trimmed[..(TelegramText.MaxLength - 2)].TrimEnd() + " …";
    }
}
