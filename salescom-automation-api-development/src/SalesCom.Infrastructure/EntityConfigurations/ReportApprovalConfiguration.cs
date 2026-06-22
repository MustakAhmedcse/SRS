namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Approvals;

public sealed class ReportApprovalConfiguration : IEntityTypeConfiguration<ReportApproval>
{
    public void Configure(EntityTypeBuilder<ReportApproval> builder)
    {
        builder.ToTable("report_approvals");
        builder.ConfigureKeyAndVersion();

        builder.Property(r => r.ReportSetupId).HasColumnName("report_setup_id").IsRequired();
        builder.Property(r => r.ApprovalFlowId).HasColumnName("approval_flow_id").IsRequired();
        builder.Property(r => r.CurrentLevelOrder).HasColumnName("current_level_order").IsRequired();
        builder.Property(r => r.OverallStatus).HasColumnName("overall_status").IsRequired();
        builder.Property(r => r.InitiatedBy).HasColumnName("initiated_by").HasMaxLength(200).IsRequired();
        builder.Property(r => r.InitiatedAt).HasColumnName("initiated_at").IsRequired();

        // An approval runs a report setup against a flow; both are protected — an in-flight or
        // historical approval keeps its setup and flow alive.
        builder.HasOne(r => r.ReportSetup)
            .WithMany()
            .HasForeignKey(r => r.ReportSetupId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne<ApprovalFlow>()
            .WithMany()
            .HasForeignKey(r => r.ApprovalFlowId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasIndex(r => r.ReportSetupId).HasDatabaseName("ix_report_approvals_report_setup");
        builder.HasIndex(r => r.ApprovalFlowId).HasDatabaseName("ix_report_approvals_flow");
    }
}
