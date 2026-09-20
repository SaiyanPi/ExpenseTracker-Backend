using ExpenseTracker.Domain.SharedKernel;

namespace ExpenseTracker.Application.DTOs.Notification;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public NotificationType Type { get; set; }
    public string Title { get; set; } = null!;
    public string Message { get; set; } = null!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public Guid? RelatedEntityId { get; set; }
}