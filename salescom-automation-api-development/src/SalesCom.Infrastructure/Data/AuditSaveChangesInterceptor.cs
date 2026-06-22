namespace SalesCom.Infrastructure.Data;

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using SalesCom.Application.Interfaces;
using SalesCom.Domain.Entities.Auditing;
using SalesCom.Domain.Enums;
using SalesCom.Domain.Interfaces;
using SalesCom.Infrastructure.Configurations;

/// <summary>
/// EF Core save interceptor that records a full before/after change-audit trail. For every
/// inserted, updated or deleted entity it adds an <see cref="AuditLog"/> row to the same save, so
/// the trail commits in the same transaction as the change that produced it. Each row is tagged with
/// the configured <see cref="ObservabilityConfiguration.ApplicationName"/> so fleet components share
/// one audit store. The log tables themselves
/// (<see cref="AuditLog"/>, <see cref="LoginLog"/>) are never audited.
/// </summary>
internal sealed class AuditSaveChangesInterceptor(
    ICurrentUser currentUser,
    IClock clock,
    IOptions<ObservabilityConfiguration> observabilityOptions) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions SnapshotOptions = new()
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _applicationName = observabilityOptions.Value.ApplicationName;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is not null)
        {
            WriteAuditTrail(eventData.Context);
        }

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            WriteAuditTrail(eventData.Context);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteAuditTrail(DbContext context)
    {
        context.ChangeTracker.DetectChanges();

        var tracked = context.ChangeTracker.Entries()
            // Never audit the log tables themselves.
            .Where(e => e.Entity is not AuditLog and not LoginLog)
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (tracked.Count == 0)
        {
            return;
        }

        // The central user id is a text identifier, not a Guid — leave ChangedByUserId null and keep
        // the human-readable login name in ChangedBy.
        var changedBy = currentUser.IsAuthenticated && !string.IsNullOrEmpty(currentUser.UserName)
            ? currentUser.UserName
            : "system";
        Guid? changedByUserId = null;
        var changedOnUtc = clock.UtcNow;

        foreach (var entry in tracked)
        {
            context.Add(BuildAuditLog(entry, _applicationName, changedBy, changedByUserId, changedOnUtc));
        }
    }

    private static AuditLog BuildAuditLog(
        EntityEntry entry,
        string applicationName,
        string changedBy,
        Guid? changedByUserId,
        DateTimeOffset changedOnUtc)
    {
        var action = entry.State switch
        {
            EntityState.Added => AuditAction.Created,
            EntityState.Deleted => AuditAction.Deleted,
            _ => AuditAction.Updated,
        };

        var log = new AuditLog
        {
            ApplicationName = applicationName,
            EntityName = entry.Metadata.ClrType.Name,
            EntityId = ResolveKey(entry),
            ActionType = action,
            ChangedBy = changedBy,
            ChangedByUserId = changedByUserId,
            ChangedAt = changedOnUtc,
            OldValues = action == AuditAction.Created ? null : Snapshot(entry, original: true),
            NewValues = action == AuditAction.Deleted ? null : Snapshot(entry, original: false),
        };

        if (action == AuditAction.Updated)
        {
            var columns = entry.Properties
                .Where(p => p.IsModified)
                .Select(p => p.Metadata.Name)
                .ToArray();
            log.ChangedColumns = columns.Length > 0 ? string.Join(", ", columns) : null;
        }

        return log;
    }

    private static string ResolveKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null)
        {
            return string.Empty;
        }

        return string.Join(", ", key.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? string.Empty));
    }

    private static JsonDocument Snapshot(EntityEntry entry, bool original)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in entry.Properties)
        {
            var value = original ? property.OriginalValue : property.CurrentValue;
            values[property.Metadata.Name] = value is JsonDocument document ? document.RootElement : value;
        }

        return JsonSerializer.SerializeToDocument(values, SnapshotOptions);
    }
}
