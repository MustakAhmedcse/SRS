namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Reporting;

public sealed class SectionWiseReportSqlConfiguration : IEntityTypeConfiguration<SectionWiseReportSql>
{
    public void Configure(EntityTypeBuilder<SectionWiseReportSql> builder)
    {
        builder.ToTable("section_wise_report_sqls");
        builder.ConfigureKeyAndVersion();

        builder.Property(s => s.ReportSetupId).HasColumnName("report_setup_id").IsRequired();
        builder.Property(s => s.StageOrder).HasColumnName("stage_order").IsRequired();
        builder.Property(s => s.SqlText).HasColumnName("sql_text");

        // SQL sections belong to a report setup; removing the setup removes its sections.
        builder.HasOne(s => s.ReportSetup)
            .WithMany()
            .HasForeignKey(s => s.ReportSetupId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        // Stage order is unique within a setup — enforces the template ordering and serves the lookup.
        builder.HasIndex(s => new { s.ReportSetupId, s.StageOrder })
            .HasDatabaseName("ux_section_wise_report_sqls_setup_order").IsUnique();
    }
}
