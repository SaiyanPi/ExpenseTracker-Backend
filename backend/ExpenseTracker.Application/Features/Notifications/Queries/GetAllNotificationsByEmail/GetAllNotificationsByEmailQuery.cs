using ExpenseTracker.Application.Common.Pagination;
using ExpenseTracker.Application.DTOs.Notification;
using MediatR;

namespace ExpenseTracker.Application.Features.Notifications.Queries.GetAllNotificationsByEmail;

public record GetAllNotificationsByEmailQuery(SearchPagedQuery Paging) : IRequest<PagedResult<NotificationDto>>;
