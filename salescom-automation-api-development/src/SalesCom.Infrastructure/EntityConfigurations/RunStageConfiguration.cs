namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Reporting;

public sealed class RunStageConfiguration : IEntityTypeConfiguration<RunStage>
{
    public void Configure(EntityTypeBuilder<RunStage> builder)
    {
        builder.ToTable("run_stages");
        builder.ConfigureKeyAndVersion();

        builder.Property(s => s.RunId).HasColumnName("run_id").IsRequired();
        builder.Property(s => s.SqlText).HasColumnName("sql_text").IsRequired();
        builder.Property(s => s.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(s => s.RunStatus)
            .HasColumnName("run_status").HasConversion<int>().HasMaxLength(50).IsRequired();
        builder.Property(s => s.StartedAt).HasColumnName("started_at");
        builder.Property(s => s.EndedAt).HasColumnName("ended_at");
        builder.Property(s => s.DocumentType).HasColumnName("document_type").HasMaxLength(100);
        builder.Property(s => s.Bucket).HasColumnName("bucket").HasMaxLength(200);
        builder.Property(s => s.ObjectUrl).HasColumnName("object_url").HasMaxLength(1000);
        builder.Property(s => s.FileName).HasColumnName("file_name").HasMaxLength(300);
        builder.Property(s => s.FileGeneratedAt).HasColumnName("file_generated_at");
        builder.Property(s => s.OutputTableName).HasColumnName("output_table_name").HasMaxLength(200);
        builder.Property(s => s.CleanupStatus)
            .HasColumnName("cleanup_status").HasConversion<int>().HasMaxLength(50).IsRequired();

        // FK column is run_id (not the convention-default report_run_id), so map the ReportRun
        // navigation explicitly — otherwise EF infers a shadow report_run_id column.
        builder.HasOne(s => s.ReportRun)
            .WithMany()
            .HasForeignKey(s => s.RunId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasIndex(s => s.RunId).HasDatabaseName("ix_run_stages_run");
    }
}
