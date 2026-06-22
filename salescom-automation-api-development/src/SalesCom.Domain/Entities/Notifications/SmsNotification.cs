namespace SalesCom.Domain.Entities.Notifications;

using SalesCom.Domain.Common;
using SalesCom.Domain.Enums;

/// <summary>One outbound SMS notification, with its delivery state and attempt history.</summary>
public sealed class SmsNotification : EntityBase<long>
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Messages { get; set; } = string.Empty;

    public NotificationStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset? SentAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
