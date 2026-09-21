using MediatR;

namespace ExpenseTracker.Application.Features.Notifications.Commands.MarkAsRead;

public record MarkAsReadCommand(Guid NotificationId): IRequest<Unit>;