namespace SalesCom.Domain.Entities.Reporting;

using SalesCom.Domain.Common;
using SalesCom.Domain.Entities.Channels;

/// <summary>A per-channel commission amount produced by a final <see cref="ReportRun"/>.</summary>
public sealed class FinalCommission : EntityBase<long>
{
    public long ReportRunId { get; set; }

    public long ChannelId { get; set; }

    public string ChannelCode { get; set; } = string.Empty;

    public string Msisdn { get; set; } = string.Empty;

    public decimal CommissionAmount { get; set; }

    //Naviation Properties

    public Channel? Channel { get; set; }

    public ReportRun? ReportRun { get; set; }
}
