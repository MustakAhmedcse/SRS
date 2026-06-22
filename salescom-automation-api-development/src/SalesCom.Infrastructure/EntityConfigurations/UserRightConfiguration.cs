namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Identity;

public sealed class UserRightConfiguration : IEntityTypeConfiguration<UserRight>
{
    public void Configure(EntityTypeBuilder<UserRight> builder)
    {
        builder.ToTable("user_rights");
        builder.ConfigureKeyAndVersion();

        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.RightsCode).HasColumnName("rights_code").IsRequired();

        // A right belongs to a user; removing the user removes their grants.
        builder.HasOne(r => r.User)
            .WithMany(u => u.UserRights)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        // One row per (user, right) — prevents duplicate grants; also serves the per-user lookup.
        builder.HasIndex(r => new { r.UserId, r.RightsCode }).HasDatabaseName("ux_user_rights_user_right").IsUnique();
    }
}
