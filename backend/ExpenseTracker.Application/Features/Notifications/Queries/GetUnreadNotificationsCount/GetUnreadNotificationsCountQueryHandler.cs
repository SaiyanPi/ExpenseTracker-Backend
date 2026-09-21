using ExpenseTracker.Application.Common.Interfaces.Services;
using ExpenseTracker.Domain.Interfaces.Repositories;
using MediatR;

namespace ExpenseTracker.Application.Features.Notifications.Queries.GetUnreadNotificationsCount;

public class GetUnreadNotificationsCountQueryHandler : IRequestHandler<GetUnreadNotificationsCountQuery, int>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserAccessor _userAccessor;

    public GetUnreadNotificationsCountQueryHandler(
        INotificationRepository notificationRepository, 
        IUserAccessor userAccessor)
    {
        _notificationRepository = notificationRepository;
        _userAccessor = userAccessor;
    }

    public async Task<int> Handle(
        GetUnreadNotificationsCountQuery request, 
        CancellationToken cancellationToken)
    {        
        var userId = _userAccessor.UserId;
        return await _notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
    }
}