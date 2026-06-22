namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Notifications;

public sealed class SmsNotificationConfiguration : IEntityTypeConfiguration<SmsNotification>
{
    public void Configure(EntityTypeBuilder<SmsNotification> builder)
    {
        builder.ToTable("sms_notifications");
        builder.ConfigureKeyAndVersion();

        builder.Property(n => n.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30).IsRequired();
        builder.Property(n => n.Messages).HasColumnName("messages").IsRequired();
        builder.Property(n => n.Status).HasColumnName("status").IsRequired();
        builder.Property(n => n.AttemptCount).HasColumnName("attempt_count").IsRequired();
        builder.Property(n => n.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
        builder.Property(n => n.SentAt).HasColumnName("sent_at");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();
    }
}
