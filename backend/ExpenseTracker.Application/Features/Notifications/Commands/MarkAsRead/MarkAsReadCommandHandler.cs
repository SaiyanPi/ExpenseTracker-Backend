using ExpenseTracker.Application.Common.Interfaces.Services;
using ExpenseTracker.Domain.Interfaces.Repositories;
using MediatR;

namespace ExpenseTracker.Application.Features.Notifications.Commands.MarkAsRead;

public class MarkAsReadCommandHandler: IRequestHandler<MarkAsReadCommand, Unit>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserAccessor _userAccessor;

    public MarkAsReadCommandHandler(
        INotificationRepository notificationRepository,
        IUserAccessor userAccessor)
    {
        _notificationRepository = notificationRepository;
        _userAccessor = userAccessor;
    }

    public async Task<Unit> Handle(MarkAsReadCommand request, CancellationToken cancellationToken)
    {
        var userId = _userAccessor.UserId;

        await _notificationRepository.MarkAsReadAsync(
            request.NotificationId,
            userId,
            cancellationToken);

        return Unit.Value;
    }
}