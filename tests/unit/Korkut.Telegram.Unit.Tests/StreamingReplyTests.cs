using Korkut.Telegram.Services;

namespace Korkut.Telegram.Unit.Tests;

/// <summary>How a reply grows on screen: one message opened, then edited, then finished.</summary>
public class StreamingReplyTests
{
    /// <summary>A chat that keeps each message's current text, and counts the calls.</summary>
    private sealed class Chat : ITelegramApi
    {
        public List<string> Messages { get; } = [];

        public int Edits { get; private set; }

        public bool FailEdits { get; set; }

        public Task<long> SendMessageAsync(string token, long chatId, string text, CancellationToken ct = default)
        {
            Messages.Add(text);
            return Task.FromResult((long)Messages.Count);
        }

        public Task EditMessageTextAsync(string token, long chatId, long messageId, string text, CancellationToken ct = default)
        {
            if (FailEdits)
            {
                throw new TelegramApiException(400, "Bad Request: message can't be edited");
            }

            Edits++;
            Messages[(int)messageId - 1] = text;
            return Task.CompletedTask;
        }

        public Task<TelegramUser> GetMeAsync(string token, CancellationToken ct = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<TelegramUpdate>> GetUpdatesAsync(string token, long offset, int timeoutSeconds, CancellationToken ct = default) => throw new NotSupportedException();

        public Task SendTypingAsync(string token, long chatId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<int> GetChatMemberCountAsync(string token, long chatId, CancellationToken ct = default) => Task.FromResult(2);

        public Task<byte[]> DownloadFileAsync(string token, string fileId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    [Fact]
    public async Task The_first_words_open_a_message_that_then_grows()
    {
        var chat = new Chat();
        var reply = new StreamingReply(chat, "t", 1, TimeSpan.Zero);

        await reply.AppendAsync("İhtar");
        await reply.AppendAsync("name süresi");
        await reply.AppendAsync(" 30 gündür.");
        await reply.FinishAsync("İhtarname süresi 30 gündür.");

        Assert.Equal(["İhtarname süresi 30 gündür."], chat.Messages);
        Assert.Equal(2, chat.Edits); // the finished text was already on screen
    }

    [Fact]
    public async Task Edits_are_throttled_and_the_finished_reply_still_lands()
    {
        var chat = new Chat();
        var reply = new StreamingReply(chat, "t", 1, TimeSpan.FromHours(1));

        foreach (var word in "bir iki üç dört".Split(' '))
        {
            await reply.AppendAsync(word + " ");
        }

        await reply.FinishAsync("bir iki üç dört");

        Assert.Equal(["bir iki üç dört"], chat.Messages);
        Assert.Equal(1, chat.Edits); // only the final one
    }

    [Fact]
    public async Task A_long_reply_continues_in_new_messages()
    {
        var chat = new Chat();
        var reply = new StreamingReply(chat, "t", 1, TimeSpan.Zero);
        var paragraph = new string('a', 3000);
        var text = $"{paragraph}\n\n{paragraph}";

        await reply.AppendAsync(text);
        Assert.EndsWith(" …", chat.Messages[0]);
        Assert.True(chat.Messages[0].Length <= 4096);

        await reply.FinishAsync(text);

        Assert.Equal([paragraph, paragraph], chat.Messages);
    }

    [Fact]
    public async Task If_the_message_cannot_be_edited_the_reply_is_sent_anew()
    {
        var chat = new Chat();
        var reply = new StreamingReply(chat, "t", 1, TimeSpan.Zero);
        await reply.AppendAsync("Taslak");
        chat.FailEdits = true;

        await reply.FinishAsync("Taslak hazır.");

        Assert.Equal(["Taslak", "Taslak hazır."], chat.Messages);
    }

    [Fact]
    public async Task Without_any_streamed_words_the_reply_is_simply_sent()
    {
        var chat = new Chat();

        await new StreamingReply(chat, "t", 1).FinishAsync("Kısa yanıt");

        Assert.Equal(["Kısa yanıt"], chat.Messages);
    }
}
