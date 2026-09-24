using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Domain.Interfaces.Repositories;

public interface INotificationRepository
{
    Task<(IReadOnlyList<Notification> Notifications, int TotalCount)> GetNotificationsByEmailAsync(
        string userId,
        int skip,
        int take,
        string? sortBy = null,
        bool sortDesc = false,
        string? search = null,
        CancellationToken cancellationToken = default);
    
    // Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(string userId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid notificationId, string userId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTime cutOffDate, CancellationToken cancellationToken = default);
}