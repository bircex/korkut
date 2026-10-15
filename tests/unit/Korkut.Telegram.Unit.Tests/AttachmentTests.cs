using Korkut.Telegram.Services;

namespace Korkut.Telegram.Unit.Tests;

public class AttachmentTests
{
    private static TelegramMessage Message(TelegramDocument? document = null, IReadOnlyList<TelegramPhotoSize>? photo = null)
        => new(42, null, new TelegramChat(1, "private"), null, "özetle", document, photo);

    [Fact]
    public void A_photo_is_taken_at_its_largest_size()
    {
        var attachment = TelegramUpdateHandler.Attachment(Message(photo:
        [
            new TelegramPhotoSize("small", 90, 120, 2_000),
            new TelegramPhotoSize("large", 960, 1280, 180_000),
            new TelegramPhotoSize("medium", 320, 427, 20_000),
        ]));

        Assert.Equal(new TelegramAttachment("large", "foto-42.jpg", "image/jpeg", 180_000), attachment);
    }

    [Fact]
    public void A_document_keeps_its_name_and_type()
        => Assert.Equal(
            new TelegramAttachment("f1", "dilekce.pdf", "application/pdf", 1234),
            TelegramUpdateHandler.Attachment(Message(new TelegramDocument("f1", "dilekce.pdf", "application/pdf", 1234))));

    [Fact]
    public void Plain_text_has_no_attachment()
        => Assert.Null(TelegramUpdateHandler.Attachment(Message()));
}
