using AutoMapper;
using ExpenseTracker.Application.Common.Interfaces.Services;
using ExpenseTracker.Application.Common.Pagination;
using ExpenseTracker.Application.DTOs.Notification;
using ExpenseTracker.Domain.Interfaces.Repositories;
using MediatR;

namespace ExpenseTracker.Application.Features.Notifications.Queries.GetAllNotificationsByEmail;

public class GetAllNotificationsByEmailQueryHandler : IRequestHandler<GetAllNotificationsByEmailQuery, PagedResult<NotificationDto>>
{
    private readonly INotificationRepository _notificationRepository;
    private readonly IUserAccessor _userAccessor;
    private readonly IMapper _mapper;

    public GetAllNotificationsByEmailQueryHandler(
        INotificationRepository notificationRepository, 
        IUserAccessor userAccessor, 
        IMapper mapper)
    {
        _notificationRepository = notificationRepository;
        _userAccessor = userAccessor;
        _mapper = mapper;
    }

    public async Task<PagedResult<NotificationDto>> Handle(
        GetAllNotificationsByEmailQuery request, 
        CancellationToken cancellationToken)
    {        
        var userId = _userAccessor.UserId;
        var query = request.Paging;

        var (notifications, totalCount) = await _notificationRepository.GetNotificationsByEmailAsync(
            userId,
            skip: query.Skip,
            take: query.EffectivePageSize,
            sortBy: query.SortBy,
            sortDesc: query.SortDesc,
            search: query.Search,
            cancellationToken: cancellationToken);
        
        var mappedNotifications = _mapper.Map<IReadOnlyList<NotificationDto>>(notifications);

        var result = new PagedResult<NotificationDto>(
            mappedNotifications,
            totalCount,
            query.EffectivePage,
            query.EffectivePageSize);

        return result;
    }
}