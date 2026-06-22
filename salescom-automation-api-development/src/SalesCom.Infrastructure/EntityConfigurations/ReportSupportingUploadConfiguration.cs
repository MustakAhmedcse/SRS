namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Reporting;

public sealed class ReportSupportingUploadConfiguration : IEntityTypeConfiguration<ReportSupportingUpload>
{
    public void Configure(EntityTypeBuilder<ReportSupportingUpload> builder)
    {
        builder.ToTable("report_supporting_uploads");
        builder.ConfigureKeyAndVersion();

        builder.Property(u => u.ReportSetupId).HasColumnName("report_setup_id").IsRequired();
        builder.Property(u => u.DbTableName).HasColumnName("db_table_name").HasMaxLength(200).IsRequired();
        builder.Property(u => u.DbSchema).HasColumnName("db_schema").HasMaxLength(2000).IsRequired();
        builder.Property(u => u.ObjectBucket).HasColumnName("object_bucket").HasMaxLength(200).IsRequired();
        builder.Property(u => u.ObjectKey).HasColumnName("object_key").HasMaxLength(500).IsRequired();
        builder.Property(u => u.FileName).HasColumnName("file_name").HasMaxLength(300).IsRequired();
        builder.Property(u => u.RowCount).HasColumnName("row_count");
        builder.Property(u => u.UploadedAt).HasColumnName("uploaded_at").IsRequired();
        builder.Property(u => u.UploadedBy).HasColumnName("uploaded_by").HasMaxLength(200).IsRequired();

        // Supporting uploads belong to a report setup; removing the setup removes its uploads.
        builder.HasOne(u => u.ReportSetup)
            .WithMany()
            .HasForeignKey(u => u.ReportSetupId)
            .OnDelete(DeleteBehavior.ClientNoAction);

        builder.HasIndex(u => u.ReportSetupId).HasDatabaseName("ix_report_supporting_uploads_report_setup");
    }
}
