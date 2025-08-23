using FireAlarmApplication.Shared.Contracts.Enums;

public class NotificationMessage
{
    public Guid UserAlertId { get; set; }
    public Guid UserId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public AlertSeverity Priority { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Metadata { get; set; } = new();
    public int RetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
}