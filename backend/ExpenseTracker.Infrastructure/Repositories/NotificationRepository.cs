using ExpenseTracker.Application.Common.Pagination;
using ExpenseTracker.Domain.Entities;
using ExpenseTracker.Domain.Interfaces.Repositories;
using ExpenseTracker.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Infrastructure.Repositories;

public class NotificationRepository : INotificationRepository
{
    private readonly ExpenseTrackerDbContext _dbContext;
    public NotificationRepository(ExpenseTrackerDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<(IReadOnlyList<Notification> Notifications, int TotalCount)> GetNotificationsByEmailAsync(
        string userId,
        int skip,
        int take,
        string? sortBy = null,
        bool sortDesc = false,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications
            .Where(n => n.UserId == userId)
            .AsNoTracking()
            .AsQueryable();
        
        // Search
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();

            query = query.Where(n =>
                n.Title.Contains(search));
        }

        // Total count after search
        var totalCount = await query
            .CountAsync(cancellationToken);
        
        // Sorting
        query = query.ApplySorting(sortBy, sortDesc);

        // Pagination
        var notifications = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (notifications, totalCount);
    }

    // public async Task<Notification?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    // {
    //     var notification = await _dbContext.Notifications.FindAsync(id, cancellationToken);
    //     return notification;
    // }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        await _dbContext.Notifications.AddAsync(notification, cancellationToken);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<int> GetUnreadCountAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .CountAsync(
                n => n.UserId == userId && !n.IsRead,
                cancellationToken);
    }

    public async Task MarkAsReadAsync(Guid notificationId, string userId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications.FirstOrDefaultAsync(
            n =>
                n.Id == notificationId &&
                n.UserId == userId &&
                !n.IsRead, cancellationToken);

        if (notification is null)
            return;

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        var readAt = DateTime.UtcNow;

        await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(n => n.IsRead, true)
                    .SetProperty(n => n.ReadAt, readAt),
                    cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutOffDate,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .Where(n => n.CreatedAt < cutOffDate)
            .ExecuteDeleteAsync(cancellationToken);
    }
}