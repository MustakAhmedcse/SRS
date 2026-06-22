namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Reporting;
using SalesCom.Infrastructure.Data;

public sealed class ReportSetupConfiguration : IEntityTypeConfiguration<ReportSetup>
{
    public void Configure(EntityTypeBuilder<ReportSetup> builder)
    {
        builder.ToTable("report_setups");
        builder.ConfigureKeyAndVersion();

        builder.Property(r => r.ReportName).HasColumnName("report_name").HasMaxLength(200).IsRequired();
        builder.Property(r => r.ReportType).HasColumnName("report_type").HasMaxLength(100).IsRequired();
        builder.Property(r => r.ChannelTypeId).HasColumnName("channel_type_id").IsRequired();
        builder.Property(r => r.CommissionCycle).HasColumnName("commission_cycle").HasMaxLength(100).IsRequired();
        builder.Property(r => r.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(r => r.EndDate).HasColumnName("end_date").IsRequired();
        builder.Property(r => r.IsSetupComplete).HasColumnName("is_setup_complete").IsRequired();
        builder.Property(r => r.IsRecurrent).HasColumnName("is_recurrent").IsRequired();
        builder.Property(r => r.RecurrentType)
            .HasColumnName("recurrent_type").HasConversion<int>().HasMaxLength(100).IsRequired();
        builder.Property(r => r.IsEvDisbursement).HasColumnName("is_ev_disbursement").IsRequired();
        builder.Property(r => r.EvDisbursementTime).HasColumnName("ev_disbursement_time");
        builder.Property(r => r.IsPosDisbursement).HasColumnName("is_pos_disbursement").IsRequired();

        builder.Property(r => r.Definition)
            .HasColumnName("definition").HasColumnType("jsonb").HasConversion<JsonDocumentConverter>();

        builder.Property(r => r.RunStartDate).HasColumnName("run_start_date");
        builder.Property(r => r.RunEndDate).HasColumnName("run_end_date");
        builder.Property(r => r.IsReportStop).HasColumnName("is_report_stop").IsRequired();
        builder.Property(r => r.SmsContent).HasColumnName("sms_content");

        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);

        builder.Property(r => r.ApprovalFlowId).HasColumnName("approval_flow_id").IsRequired();

        // Channel FK column is channel_type_id (not the convention-default channel_id), so map it
        // explicitly — otherwise EF infers a shadow channel_id column for the Channel navigation.
        builder.HasOne(r => r.Channel)
            .WithMany()
            .HasForeignKey(r => r.ChannelTypeId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(r => r.ApprovalFlow)
            .WithMany()
            .HasForeignKey(r => r.ApprovalFlowId)
            .OnDelete(DeleteBehavior.ClientNoAction);
    }
}
