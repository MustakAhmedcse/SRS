namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Entities.DataSources;

public sealed class DataSourceConfiguration : IEntityTypeConfiguration<DataSource>
{
    public void Configure(EntityTypeBuilder<DataSource> builder)
    {
        builder.ToTable("data_sources");
        builder.ConfigureKeyAndVersion();

        builder.Property(d => d.SourceTableName).HasColumnName("source_table_name").HasMaxLength(200).IsRequired();
        builder.HasIndex(d => d.SourceTableName).HasDatabaseName("ux_data_sources_source_table").IsUnique();

        builder.Property(d => d.TableDescription).HasColumnName("table_description").HasMaxLength(1000);
        builder.Property(d => d.IsActive).HasColumnName("is_active").IsRequired();

        builder.Property(d => d.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at");
        builder.Property(d => d.CreatedBy).HasColumnName("created_by").HasMaxLength(200).IsRequired();
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by").HasMaxLength(200);
    }
}
