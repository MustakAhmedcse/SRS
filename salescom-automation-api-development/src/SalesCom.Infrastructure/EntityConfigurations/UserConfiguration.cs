namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.ConfigureKeyAndVersion();

        builder.Property(u => u.UserName).HasColumnName("user_name").HasMaxLength(200).IsRequired();

        builder.Property(u => u.UserId).HasColumnName("user_id").HasMaxLength(100).IsRequired();
        builder.HasIndex(u => u.UserId).HasDatabaseName("ux_users_user_id").IsUnique();

        builder.Property(u => u.FullName).HasColumnName("full_name").HasMaxLength(300).IsRequired();
        builder.Property(u => u.MobileNo).HasColumnName("mobile_no").HasMaxLength(30).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(u => u.Department).HasColumnName("department").HasMaxLength(200).IsRequired();

        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
        builder.Property(u => u.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
    }
}
