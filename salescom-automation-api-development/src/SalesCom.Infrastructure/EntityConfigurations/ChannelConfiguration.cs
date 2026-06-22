namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Channels;

public sealed class ChannelConfiguration : IEntityTypeConfiguration<Channel>
{
    public void Configure(EntityTypeBuilder<Channel> builder)
    {
        builder.ToTable("channels");
        builder.ConfigureKeyAndVersion();

        builder.Property(c => c.ChannelName).HasColumnName("channel_name").HasMaxLength(200).IsRequired();
        builder.HasIndex(c => c.ChannelName).HasDatabaseName("ux_channels_channel_name").IsUnique();
    }
}
