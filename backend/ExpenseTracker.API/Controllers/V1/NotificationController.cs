using AutoMapper;
using ExpenseTracker.API.Contracts.V1.Common.Pagination;
using ExpenseTracker.API.Contracts.V1.Notification;
using ExpenseTracker.Application.Common.Pagination;
using ExpenseTracker.Application.Features.Notifications.Commands.MarkAllAsRead;
using ExpenseTracker.Application.Features.Notifications.Commands.MarkAsRead;
using ExpenseTracker.Application.Features.Notifications.Queries.GetAllNotificationsByEmail;
using ExpenseTracker.Application.Features.Notifications.Queries.GetUnreadNotificationsCount;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseTracker.API.Controllers.V1;

[ApiController]
[Authorize]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IMapper _mapper;

    public NotificationsController(
        IMediator mediator,
        IMapper mapper)
    {
        _mediator = mediator;
        _mapper = mapper;
    }

    // GET: api/v1/notifications
    [HttpGet]
    public async Task<IActionResult> GetNotificationsByEmail(
        [FromQuery] SearchPagedResultRequestV1 searchPagedResultRequest,
        CancellationToken cancellationToken = default)
    {
        var query = new GetAllNotificationsByEmailQuery(
            new SearchPagedQuery(
                searchPagedResultRequest.search,
                searchPagedResultRequest.page,
                searchPagedResultRequest.pageSize,
                searchPagedResultRequest.sortBy,
                searchPagedResultRequest.sortDesc));

        var notificationsByEmail = await _mediator.Send(query, cancellationToken);

        var response = new PagedResultResponseV1<NotificationResponseV1>
        {
            Items = _mapper.Map<List<NotificationResponseV1>>(notificationsByEmail.Items),

            TotalCount = notificationsByEmail.TotalCount,
            Page = notificationsByEmail.Page,
            PageSize = notificationsByEmail.PageSize,
            TotalPages = notificationsByEmail.TotalPages,
            HasNext = notificationsByEmail.HasNext,
            HasPrevious = notificationsByEmail.HasPrevious
        };
        return Ok(response);
    }

    // GET: api/v1/notifications/unread-count
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var query = new GetUnreadNotificationsCountQuery();
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }

    // GET: api/v1/notifications/{notificationId}/read
    [HttpPatch("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        var command = new MarkAsReadCommand(notificationId);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    // GET: api/v1/notifications/read-all

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var command = new MarkAllAsReadCommand();
        await _mediator.Send(command, cancellationToken);

        return NoContent();
    }
}