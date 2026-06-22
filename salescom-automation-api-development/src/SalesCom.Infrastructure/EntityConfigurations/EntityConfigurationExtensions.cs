namespace SalesCom.Infrastructure.EntityConfigurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalesCom.Domain.Common;

/// <summary>
/// Shared mapping for the bits every entity has in common: the <c>bigint</c> identity primary key
/// (DB-generated, so handlers never set it) and the Postgres <c>xmin</c> concurrency token. Keeps the
/// per-entity configs focused on their own columns.
/// </summary>
internal static class EntityConfigurationExtensions
{
    public static void ConfigureKeyAndVersion<T>(this EntityTypeBuilder<T> builder)
        where T : EntityBase<long>
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();

        builder.Property(e => e.Version)
            .HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
    }
}
