namespace SalesCom.Domain.Enums;

/// <summary>Whether a <see cref="Entities.Reporting.ReportRun"/> is a trial (demo) or the final, disbursable run.</summary>
public enum ReportRunType
{
    Demo = 1,
    Final = 2,
}
