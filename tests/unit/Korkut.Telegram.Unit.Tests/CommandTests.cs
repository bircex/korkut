using Korkut.Telegram.Services;

namespace Korkut.Telegram.Unit.Tests;

public class CommandTests
{
    [Theory]
    [InlineData("/start abc_-1", "/start", "abc_-1")]
    [InlineData("/start", "/start", "")]
    [InlineData("/YENI", "/yeni", "")]
    [InlineData("/yeni@KorkutBot", "/yeni", "")]
    [InlineData("Faiz ne kadar?", "", "")]
    [InlineData("/yardim\nsatır", "/yardim", "satır")]
    public void Commands_are_read_the_way_Telegram_sends_them(string text, string command, string argument)
        => Assert.Equal((command, argument), TelegramUpdateHandler.ParseCommand(text));
}
