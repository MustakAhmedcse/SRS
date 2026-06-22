namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Notifications;

public sealed class EmailNotificationConfiguration : IEntityTypeConfiguration<EmailNotification>
{
    public void Configure(EntityTypeBuilder<EmailNotification> builder)
    {
        builder.ToTable("email_notifications");
        builder.ConfigureKeyAndVersion();

        builder.Property(n => n.ToAddress).HasColumnName("to_address").HasMaxLength(256).IsRequired();
        builder.Property(n => n.Cc).HasColumnName("cc").HasMaxLength(1000);
        builder.Property(n => n.Bcc).HasColumnName("bcc").HasMaxLength(1000);
        builder.Property(n => n.Subject).HasColumnName("subject").HasMaxLength(500);
        builder.Property(n => n.Body).HasColumnName("body").IsRequired();
        builder.Property(n => n.FromAddress).HasColumnName("from_address").HasMaxLength(256);
        builder.Property(n => n.Status).HasColumnName("status").IsRequired();
        builder.Property(n => n.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(n => n.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(n => n.SentAt).HasColumnName("sent_at");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
