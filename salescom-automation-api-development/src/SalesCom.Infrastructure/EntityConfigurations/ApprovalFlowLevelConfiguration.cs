namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Approvals;

public sealed class ApprovalFlowLevelConfiguration : IEntityTypeConfiguration<ApprovalFlowLevel>
{
    public void Configure(EntityTypeBuilder<ApprovalFlowLevel> builder)
    {
        builder.ToTable("approval_flow_levels");
        builder.ConfigureKeyAndVersion();

        builder.Property(l => l.ApprovalFlowId).HasColumnName("approval_flow_id").IsRequired();
        builder.Property(l => l.ApprovalType).HasColumnName("approval_type").IsRequired();
        builder.Property(l => l.LevelOrder).HasColumnName("level_order").IsRequired();
        builder.Property(l => l.LevelName).HasColumnName("level_name").HasMaxLength(200).IsRequired();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(l => l.UpdatedAt).HasColumnName("updated_at");
        builder.Property(l => l.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
        builder.Property(l => l.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.HasOne(l => l.ApprovalFlow)
            .WithMany(f => f.ApprovalFlowLevels)
            .HasForeignKey(l => l.ApprovalFlowId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        // Level order is unique within a flow — enforces the step ordering and serves the lookup.
        builder.HasIndex(l => new { l.ApprovalFlowId, l.LevelOrder })
            .HasDatabaseName("ux_approval_flow_levels_flow_order").IsUnique();
    }
}
