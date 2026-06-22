namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Approvals;

public sealed class ApprovalFlowConfiguration : IEntityTypeConfiguration<ApprovalFlow>
{
    public void Configure(EntityTypeBuilder<ApprovalFlow> builder)
    {
        builder.ToTable("approval_flows");
        builder.ConfigureKeyAndVersion();

        builder.Property(f => f.FlowName).HasColumnName("flow_name").HasMaxLength(200).IsRequired();
        builder.Property(f => f.Description).HasColumnName("description").HasMaxLength(1000);
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");
        builder.Property(f => f.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
        builder.Property(f => f.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
    }
}
