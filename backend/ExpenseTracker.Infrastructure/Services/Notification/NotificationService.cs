using AutoMapper;
using ExpenseTracker.Application.Common.Interfaces.Services;
using ExpenseTracker.Application.DTOs.Notification;
using ExpenseTracker.Domain.Interfaces.Repositories;
using ExpenseTracker.Domain.SharedKernel;
using Microsoft.AspNetCore.SignalR;

namespace ExpenseTracker.Infrastructure.Services.Notification;

public class NotificationService : INotificationService
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly INotificationRepository _notificationRepository;
    private readonly IMapper _mapper;


    public NotificationService(
        IHubContext<NotificationHub> hubContext,
        INotificationRepository notificationRepository,
        IMapper mapper
       )
    {
        _hubContext = hubContext;
        _notificationRepository = notificationRepository;
        _mapper = mapper;
    }

    public async Task BudgetExceededAsync(
        Guid budgetId,
        string budgetName,
        // decimal totalSpent,
        // decimal budgetAmount,
        decimal percentageUsed,
        decimal remainingAmount,
        string userId,
        CancellationToken cancellationToken = default)
    {
         var notificationDto = new NotificationDto
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = NotificationType.BudgetExceeded,
            Title = $"{budgetName} Budget Alert",
            Message =
                $"You've used {percentageUsed:N0}% of your budget. " +
                $"Rs. {remainingAmount:N2} remaining.",
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            RelatedEntityId = budgetId
        };

        var notification = _mapper.Map<ExpenseTracker.Domain.Entities.Notification>(notificationDto);

        await _notificationRepository.AddAsync(
            notification,
            cancellationToken);

        await _hubContext
            .Clients
            .User(userId)
            .SendAsync("BudgetExceeded", new
            {
                budgetId,
                budgetName,
                // totalSpent,
                // budgetAmount,
                percentageUsed,
                remainingAmount,
                exceededAt = DateTime.UtcNow
            }, cancellationToken);
    }
}