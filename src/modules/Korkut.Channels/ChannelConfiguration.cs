using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Korkut.Channels;

/// <summary>EF mapping for messaging channels (table/columns unchanged; keyed per office per kind).</summary>
public sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToTable("channels");
        builder.HasKey(x => new { x.OfficeId, x.Kind }); // one config per kind per office
        builder.Property(x => x.Kind).HasMaxLength(20);
        builder.Property(x => x.FromAddress).HasMaxLength(320);
        builder.Property(x => x.FromName).HasMaxLength(200);
        builder.Property(x => x.BaseUrl).HasMaxLength(400);
        builder.Property(x => x.LinkedId).HasMaxLength(200);
        builder.Property(x => x.LinkCode).HasMaxLength(40);
        builder.Ignore(x => x.HasSecret);
    }
}
