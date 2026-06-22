namespace SalesCom.Domain.Enums;

/// <summary>Whether a <see cref="Entities.Reporting.ReportRun"/> is a trial (demo) or the final, disbursable run.</summary>
public enum CleanupStatus
{
    Pending = 0,
    Cleaning = 1,
    Cleaned = 2,
}
