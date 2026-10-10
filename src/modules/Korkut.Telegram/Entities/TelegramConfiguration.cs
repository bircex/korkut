using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Telegram.Entities;

/// <summary>EF mapping for the office bot (one row per office).</summary>
public sealed class TelegramBotConfiguration : IEntityTypeConfiguration<TelegramBot>
{
    public void Configure(EntityTypeBuilder<TelegramBot> builder)
    {
        builder.ToTable("telegram_bots");
        builder.HasKey(x => x.OfficeId);
        builder.Property(x => x.OfficeId).ValueGeneratedNever();
        builder.Property(x => x.TokenCipher).HasMaxLength(500);
        builder.Property(x => x.BotUsername).HasMaxLength(64);
    }
}

/// <summary>EF mapping for members' links (one row per member; a chat links to one member).</summary>
public sealed class TelegramLinkConfiguration : IEntityTypeConfiguration<TelegramLink>
{
    public void Configure(EntityTypeBuilder<TelegramLink> builder)
    {
        builder.ToTable("telegram_links");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).ValueGeneratedNever();
        builder.HasIndex(x => x.OfficeId);
        builder.HasIndex(x => x.ChatId).IsUnique();
        builder.HasIndex(x => x.CodeHash).IsUnique();
        builder.Property(x => x.TelegramUsername).HasMaxLength(64);
        builder.Property(x => x.CodeHash).HasMaxLength(64);
        builder.Ignore(x => x.IsLinked);
    }
}
