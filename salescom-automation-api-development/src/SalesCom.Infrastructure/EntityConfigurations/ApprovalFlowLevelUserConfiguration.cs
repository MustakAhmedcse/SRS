namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Approvals;

public sealed class ApprovalFlowLevelUserConfiguration : IEntityTypeConfiguration<ApprovalFlowLevelUser>
{
    public void Configure(EntityTypeBuilder<ApprovalFlowLevelUser> builder)
    {
        builder.ToTable("approval_flow_level_users");
        builder.ConfigureKeyAndVersion();

        builder.Property(u => u.ApprovalFlowLevelId).HasColumnName("approval_flow_level_id").IsRequired();
        builder.Property(u => u.UserId).HasColumnName("user_id").IsRequired();

        builder.HasOne(u => u.ApprovalFlowLevel)
            .WithMany(l => l.ApprovalLevelUsers)
            .HasForeignKey(u => u.ApprovalFlowLevelId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        // Each assignment points to a local user (the surrogate users.id); the user is protected — an
        // assignment keeps the user alive.
        builder.HasOne(u => u.User)
            .WithMany(f=>f.ApprovalFlowLevelUsers)
            .HasForeignKey(u => u.UserId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasIndex(u => new { u.ApprovalFlowLevelId, u.UserId })
            .HasDatabaseName("ux_approval_flow_level_users_level_user").IsUnique();
    }
}
