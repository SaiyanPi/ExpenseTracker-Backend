namespace ExpenseTracker.Application.Common.Retention;

public class NotificationRetentionOptions
{
    public int RetentionDays { get; set; } = 62;
    public int CleanupIntervalHours { get; set; } = 24;
}