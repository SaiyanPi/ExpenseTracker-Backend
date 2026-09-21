using ExpenseTracker.Application.Common.Interfaces.Services;
using ExpenseTracker.Domain.Interfaces.Repositories;
using MediatR;

namespace ExpenseTracker.Application.Features.Notifications.Commands.MarkAllAsRead;

public sealed class MarkAllAsReadCommandHandler: IRequestHandler<MarkAllAsReadCommand, Unit>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserAccessor _userAccessor;

    public MarkAllAsReadCommandHandler(
        INotificationRepository notificationRepository,
        IUserAccessor userAccessor)
    {
        _notificationRepository = notificationRepository;
        _userAccessor = userAccessor;
    }

    public async Task<Unit> Handle(
        MarkAllAsReadCommand request,
        CancellationToken cancellationToken)
    {
        var userId = _userAccessor.UserId;

        await _notificationRepository.MarkAllAsReadAsync(userId, cancellationToken);

        return Unit.Value;
    }
}