namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Reporting;

public sealed class ReportRunConfiguration : IEntityTypeConfiguration<ReportRun>
{
    public void Configure(EntityTypeBuilder<ReportRun> builder)
    {
        builder.ToTable("report_runs");
        builder.ConfigureKeyAndVersion();

        builder.Property(r => r.ReportSetupId).HasColumnName("report_setup_id").IsRequired();
        builder.Property(r => r.RunDate).HasColumnName("run_date").IsRequired();
        builder.Property(r => r.RunType).HasColumnName("run_type").IsRequired();
        builder.Property(r => r.TriggeredBy).HasColumnName("triggered_by").HasMaxLength(200);
        builder.Property(r => r.RunStatus).HasColumnName("run_status").HasMaxLength(50).IsRequired();
        builder.Property(r => r.DisburseStatus).HasColumnName("disburse_status").HasMaxLength(50).IsRequired();
        builder.Property(r => r.StartedAt).HasColumnName("started_at");
        builder.Property(r => r.EndedAt).HasColumnName("ended_at");

        // A run records an execution of a setup; runs (and their commissions) are history, so a setup
        // with runs cannot be deleted out from under them.
        builder.HasOne(r => r.ReportSetup)
            .WithMany()
            .HasForeignKey(r => r.ReportSetupId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasIndex(r => r.ReportSetupId).HasDatabaseName("ix_report_runs_report_setup");
    }
}
