using AutoMapper;
using ExpenseTracker.Application.DTOs.Notification;
using ExpenseTracker.Domain.Entities;

namespace ExpenseTracker.Application.Common.Mappings;

public class NotificationMappingProfile : Profile
{
    public NotificationMappingProfile()
    {
        CreateMap<NotificationDto, Notification>();
        CreateMap<Notification, NotificationDto>();

    }
    
}