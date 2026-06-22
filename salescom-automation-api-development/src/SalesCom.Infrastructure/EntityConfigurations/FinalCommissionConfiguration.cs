namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Reporting;

public sealed class FinalCommissionConfiguration : IEntityTypeConfiguration<FinalCommission>
{
    public void Configure(EntityTypeBuilder<FinalCommission> builder)
    {
        builder.ToTable("final_commissions");
        builder.ConfigureKeyAndVersion();

        builder.Property(f => f.ReportRunId).HasColumnName("report_run_id").IsRequired();
        builder.Property(f => f.ChannelId).HasColumnName("channel_id").IsRequired();
        builder.Property(f => f.ChannelCode).HasColumnName("channel_code").HasMaxLength(100).IsRequired();
        builder.Property(f => f.Msisdn).HasColumnName("msisdn").HasMaxLength(20);
        builder.Property(f => f.CommissionAmount).HasColumnName("commission_amount").HasColumnType("numeric(18,4)").IsRequired();

        // A final commission is an output line of a run (no run navigation on the entity); it dies with
        // the run. The channel it pays is a protected lookup.
        builder.HasOne(f=>f.ReportRun)
            .WithMany()
            .HasForeignKey(f => f.ReportRunId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasOne(f => f.Channel)
            .WithMany()
            .HasForeignKey(f => f.ChannelId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        // One commission line per channel within a run — the composite is unique and its leading
        // report_run_id column also serves the per-run lookup, so no separate report_run index is needed.
        builder.HasIndex(f => new { f.ReportRunId, f.ChannelCode })
            .HasDatabaseName("ux_final_commissions_run_channel_code").IsUnique();
        builder.HasIndex(f => f.ChannelId).HasDatabaseName("ix_final_commissions_channel");
    }
}
