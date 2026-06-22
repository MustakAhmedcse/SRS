namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.Auditing;
using SalesCom.Infrastructure.Data;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.ConfigureKeyAndVersion();

        builder.Property(a => a.ApplicationName).HasColumnName("application_name").HasMaxLength(100).IsRequired();
        builder.Property(a => a.EntityName).HasColumnName("entity_name").HasMaxLength(200).IsRequired();
        builder.Property(a => a.EntityId).HasColumnName("entity_id").HasMaxLength(200).IsRequired();
        builder.Property(a => a.ActionType).HasColumnName("action_type").IsRequired();
        builder.Property(a => a.ChangedByUserId).HasColumnName("changed_by_user_id");
        builder.Property(a => a.ChangedBy).HasColumnName("changed_by").HasMaxLength(200).IsRequired();
        builder.Property(a => a.ChangedAt).HasColumnName("changed_at").IsRequired();
        builder.Property(a => a.ChangedColumns).HasColumnName("changed_columns");

        builder.Property(a => a.OldValues)
            .HasColumnName("old_values").HasColumnType("jsonb").HasConversion<JsonDocumentConverter>();
        builder.Property(a => a.NewValues)
            .HasColumnName("new_values").HasColumnType("jsonb").HasConversion<JsonDocumentConverter>();

        builder.HasIndex(a => new { a.EntityName, a.EntityId }).HasDatabaseName("ix_audit_logs_entity");
    }
}
