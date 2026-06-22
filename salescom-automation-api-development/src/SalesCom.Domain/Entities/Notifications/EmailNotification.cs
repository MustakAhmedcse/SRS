namespace SalesCom.Domain.Entities.Notifications;

using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;

/// <summary>One outbound notification (email or SMS), with its delivery state and attempt history.</summary>
public sealed class EmailNotification : EntityBase<long>
{
    public string ToAddress { get; set; } = string.Empty;

    public string? Cc { get; set; }

    public string? Bcc { get; set; }

    public string? Subject { get; set; }

    public string Body { get; set; } = string.Empty;

    public string? FromAddress { get; set; }

    public NotificationStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
