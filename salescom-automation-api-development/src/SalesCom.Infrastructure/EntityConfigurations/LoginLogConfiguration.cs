namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Auditing;

public sealed class LoginLogConfiguration : IEntityTypeConfiguration<LoginLog>
{
    public void Configure(EntityTypeBuilder<LoginLog> builder)
    {
        builder.ToTable("login_logs");
        builder.ConfigureKeyAndVersion();

        builder.Property(l => l.UserName).HasColumnName("user_name").HasMaxLength(200).IsRequired();
        builder.Property(l => l.FullName).HasColumnName("full_name").HasMaxLength(300).IsRequired();
        builder.Property(l => l.LoginTime).HasColumnName("login_time").IsRequired();
        builder.Property(l => l.LoginStatus).HasColumnName("login_status").IsRequired();
        builder.Property(l => l.Remarks).HasColumnName("remarks").HasMaxLength(500).IsRequired();
    }
}
