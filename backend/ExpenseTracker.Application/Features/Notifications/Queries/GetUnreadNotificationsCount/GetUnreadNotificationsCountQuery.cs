using ExpenseTracker.Application.DTOs.Category;
using MediatR;

namespace ExpenseTracker.Application.Features.Notifications.Queries.GetUnreadNotificationsCount;

public record GetUnreadNotificationsCountQuery() : IRequest<int>;