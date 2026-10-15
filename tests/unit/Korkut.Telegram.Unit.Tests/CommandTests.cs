using Korkut.Contract.Matters;
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

public class MatterLookupTests
{
    private static MatterResponse Matter(string title, string? fileNo = null)
        => new(Guid.NewGuid(), title, null, fileNo, null, null, null, "open", null, null);

    private static readonly MatterResponse[] Matters =
        [Matter("KİRA TESPİT DAVASI", "2026/41"), Matter("Kira alacağı", "2026/7"), Matter("İşçilik alacağı", "2025/300")];

    [Theory]
    [InlineData("kira tespit", "KİRA TESPİT DAVASI")]   // case and Turkish letters do not matter
    [InlineData("iscilik", "İşçilik alacağı")]
    [InlineData("2026/7", "Kira alacağı")]              // a file number names exactly one
    [InlineData("kira alacağı", "Kira alacağı")]        // an exact title beats a partial one
    public void A_query_names_one_matter(string query, string expected)
        => Assert.Equal(expected, Assert.Single(TelegramUpdateHandler.Find(Matters, query)).Title);

    [Fact]
    public void A_vague_query_names_several_and_an_unknown_one_none()
    {
        Assert.Equal(2, TelegramUpdateHandler.Find(Matters, "kira").Count);
        Assert.Empty(TelegramUpdateHandler.Find(Matters, "boşanma"));
    }
}
