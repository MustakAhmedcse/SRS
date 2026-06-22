namespace SalesCom.Domain.Enums;

/// <summary>Delivery state of a <see cref="Entities.Notifications.EmailNotification"/>.</summary>
public enum NotificationStatus
{
    Pending = 0,
    Sending = 1,
    Sent = 2,
    Failed = 3,
}
