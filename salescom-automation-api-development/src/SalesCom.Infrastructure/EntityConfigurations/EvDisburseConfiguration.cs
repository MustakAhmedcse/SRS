namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Disbursement;

public sealed class EvDisburseConfiguration : IEntityTypeConfiguration<EvDisburse>
{
    public void Configure(EntityTypeBuilder<EvDisburse> builder)
    {
        builder.ToTable("ev_disburses");
        builder.ConfigureKeyAndVersion();

        builder.Property(e => e.ReportRunId).HasColumnName("report_run_id").IsRequired();
        builder.Property(e => e.ChannelCode).HasColumnName("channel_code").HasMaxLength(100).IsRequired();
        builder.Property(e => e.EvMsisdn).HasColumnName("ev_msisdn").HasMaxLength(15).IsRequired();
        builder.Property(e => e.Amount).HasColumnName("amount").HasColumnType("numeric(18,4)").IsRequired();
        builder.Property(e => e.DisburseStatus).HasColumnName("disburse_status").HasMaxLength(50).IsRequired();
        builder.Property(e => e.DisburseAt).HasColumnName("disburse_at");

        // An EV disbursement is an output line of a run and dies with it; the paid channel is protected.
        builder.HasOne(e => e.ReportRun)
            .WithMany()
            .HasForeignKey(e => e.ReportRunId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasIndex(e => e.ReportRunId).HasDatabaseName("ix_ev_disburse_report_run");
    }
}
