using MediatR;

namespace ExpenseTracker.Application.Features.Notifications.Commands.MarkAllAsRead;

public sealed record MarkAllAsReadCommand : IRequest<Unit>;