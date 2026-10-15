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

/// <summary>EF mapping for groups bound to a matter (one matter per group).</summary>
public sealed class TelegramGroupConfiguration : IEntityTypeConfiguration<TelegramGroup>
{
    public void Configure(EntityTypeBuilder<TelegramGroup> builder)
    {
        builder.ToTable("telegram_groups");
        builder.HasKey(x => x.ChatId);
        builder.Property(x => x.ChatId).ValueGeneratedNever();
        builder.HasIndex(x => new { x.OfficeId, x.MatterId });
    }
}

/// <summary>EF mapping for the people the bot knows are in a group.</summary>
public sealed class TelegramGroupMemberConfiguration : IEntityTypeConfiguration<TelegramGroupMember>
{
    public void Configure(EntityTypeBuilder<TelegramGroupMember> builder)
    {
        builder.ToTable("telegram_group_members");
        builder.HasKey(x => new { x.ChatId, x.TelegramUserId });
        builder.HasIndex(x => x.OfficeId);
    }
}
