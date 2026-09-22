using AutoMapper;
using ExpenseTracker.API.Contracts.V1.Notification;
using ExpenseTracker.Application.DTOs.Notification;

namespace ExpenseTracker.API.Contracts.V1.Common.Mappings;

public class NotificationMappingProfile : Profile
{
    public NotificationMappingProfile()
    {
        CreateMap<NotificationResponseV1, NotificationDto>();
        CreateMap<NotificationDto, NotificationResponseV1>();

    }
    
}