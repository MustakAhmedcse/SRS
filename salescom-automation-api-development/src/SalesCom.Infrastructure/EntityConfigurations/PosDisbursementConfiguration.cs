namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Disbursement;

public sealed class PosDisbursementConfiguration : IEntityTypeConfiguration<PosDisbursement>
{
    public void Configure(EntityTypeBuilder<PosDisbursement> builder)
    {
        builder.ToTable("pos_disbursements");
        builder.ConfigureKeyAndVersion();

        builder.Property(p => p.ReportRunId).HasColumnName("report_run_id").IsRequired();
        builder.Property(p => p.DumpStatus).HasColumnName("dump_status").HasMaxLength(50).IsRequired();
        builder.Property(p => p.DisburseAt).HasColumnName("disburse_at");

        // A POS disbursement dump is an output of a run and dies with it.
        builder.HasOne(p => p.ReportRun)
            .WithMany()
            .HasForeignKey(p => p.ReportRunId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasIndex(p => p.ReportRunId).HasDatabaseName("ix_pos_disbursement_report_run");
    }
}
