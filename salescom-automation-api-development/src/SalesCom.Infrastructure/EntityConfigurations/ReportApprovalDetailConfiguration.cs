namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Approvals;

public sealed class ReportApprovalDetailConfiguration : IEntityTypeConfiguration<ReportApprovalDetail>
{
    public void Configure(EntityTypeBuilder<ReportApprovalDetail> builder)
    {
        builder.ToTable("report_approval_details");
        builder.ConfigureKeyAndVersion();

        builder.Property(d => d.ApprovalRequestId).HasColumnName("approval_request_id").IsRequired();
        builder.Property(d => d.LevelOrder).HasColumnName("level_order").IsRequired();
        builder.Property(d => d.ApprovalStatus).HasColumnName("approval_status").IsRequired();
        builder.Property(d => d.Remarks).HasColumnName("remarks").HasMaxLength(1000);
        builder.Property(d => d.ApprovalBy).HasColumnName("approval_by").HasMaxLength(200).IsRequired();
        builder.Property(d => d.ApprovalAt).HasColumnName("approval_at").IsRequired();

        // A detail line belongs to its parent approval and is removed with it.
        builder.HasOne(d => d.ApprovalRequest)
            .WithMany(r => r.ApprovalDecisions)
            .HasForeignKey(d => d.ApprovalRequestId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasIndex(d => d.ApprovalRequestId).HasDatabaseName("ix_report_approval_details_approval");
    }
}
