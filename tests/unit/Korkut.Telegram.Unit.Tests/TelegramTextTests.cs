using Korkut.Telegram.Services;

namespace Korkut.Telegram.Unit.Tests;

public class TelegramTextTests
{
    [Fact]
    public void A_short_reply_is_one_message()
        => Assert.Equal(["Merhaba"], TelegramText.Split("  Merhaba \n"));

    [Fact]
    public void A_long_reply_breaks_at_paragraphs_and_loses_nothing()
    {
        var paragraph = new string('a', 30);
        var text = string.Join("\n\n", Enumerable.Repeat(paragraph, 10));

        var parts = TelegramText.Split(text, maxLength: 100);

        Assert.All(parts, p => Assert.True(p.Length <= 100));
        Assert.All(parts, p => Assert.DoesNotContain("\n\n", p[..2] + p[^2..]));
        Assert.Equal(text.Replace("\n", string.Empty), string.Concat(parts).Replace("\n", string.Empty));
    }

    [Fact]
    public void Text_without_breaks_is_cut_hard_but_never_inside_a_character()
    {
        var text = new string('x', 99) + "😀" + new string('y', 50);

        var parts = TelegramText.Split(text, maxLength: 100);

        Assert.Equal(new string('x', 99), parts[0]);
        Assert.StartsWith("😀", parts[1]);
        Assert.Equal(text, string.Concat(parts));
    }

    [Fact]
    public void Telegram_limit_is_respected()
        => Assert.All(TelegramText.Split(string.Join(' ', Enumerable.Repeat("kelime", 3000))), p => Assert.True(p.Length <= 4096));
}
