namespace SalesCom.Domain.Enums;

/// <summary>Whether a <see cref="Entities.Reporting.ReportRun"/> is a trial (demo) or the final, disbursable run.</summary>
public enum RunStatus
{
    Pending = 0,
    Running = 1,
    Runned = 2,
    Faild = 3,
}
