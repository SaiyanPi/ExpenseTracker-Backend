using ExpenseTracker.Application.Common.Interfaces;
using ExpenseTracker.Application.Common.Interfaces.Services;
using ExpenseTracker.Application.Common.Retention;
using ExpenseTracker.Domain.Interfaces.Repositories;
using ExpenseTracker.Infrastructure.Repositories;
using ExpenseTracker.Infrastructure.Services.AuditLogsExport;
using ExpenseTracker.Infrastructure.Services.BackgroundServices;
using ExpenseTracker.Infrastructure.Services.Cache;
using ExpenseTracker.Infrastructure.Services.Email;
using ExpenseTracker.Infrastructure.Services.ExpenseExport;
using ExpenseTracker.Infrastructure.Services.Identity;
using ExpenseTracker.Infrastructure.Services.LocalProfileImageStorage;
using ExpenseTracker.Infrastructure.Services.Notification;
using ExpenseTracker.Infrastructure.Services.SecurityEventLogger;
using ExpenseTracker.Infrastructure.Services.SecurityEventLogsExport;
using ExpenseTracker.Infrastructure.Services.SMS;
using ExpenseTracker.Infrastructure.Services.UserAccessor;
using ExpenseTracker.Infrastructure.Services.UserRole;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Infrastructure.DI;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        // registering repositories
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IDashboardRepository, DashBoardRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IEntityResolverRepository, EntityResolverRepository>();
        services.AddScoped<ISecurityEventLogRepository, SecurityEventLogRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();

        // registering identity service
        services.AddScoped<IIdentityService, IdentityService>();

        // SMTP config
        services.Configure<SmtpSettings>(configuration.GetSection("SmtpSettings"));
        services.AddSingleton(resolver =>
            resolver.GetRequiredService<IOptions<SmtpSettings>>().Value);

        //AuditLog, SecurityEventLog, Notifications Retention config
        services.Configure<LogRetentionOptions>(configuration.GetSection("AuditLogRetention"));
        services.AddHostedService<AuditLogCleanupService>();
        services.AddHostedService<SecurityEventLogCleanupService>();
        services.Configure<NotificationRetentionOptions>(configuration.GetSection("NotificationRetention"));
        services.AddHostedService<NotificationCleanupService>();

        // registering email service
        services.AddScoped<IEmailService, SmtpEmailService>();
        
        // register sms sender service
        // services.AddScoped<ISmsSenderService, TwilioSmsSenderService>();
        services.AddHttpClient<ISmsSenderService, AndroidSmsGatewayService>();

        // register export service
        services.AddScoped<IExpenseExportService, ExpenseExportService>();
        services.AddScoped<IAuditLogsExportService, AuditLogsExportService>();
        services.AddScoped<ISecurityEventLogsExportService, SecurityEventLogsExportService>();

        // register user role(isAdmin) service
        services.AddScoped<IUserRoleService, UserRoleService>();

        // register profile image storage service
        services.AddScoped<IProfileImageStorageService, LocalProfileImageStorageService>();

        // registering user accessor service
        services.AddHttpContextAccessor();
        services.AddScoped<IUserAccessor, UserAccessor>();

        // register SignalR notification service
        services.AddScoped<INotificationService, NotificationService>();

        // register audit logger for security events
        services.AddScoped<ISecurityEventLoggerService, SecurityEventLoggerService>();

        // register category cache version service
        services.AddSingleton<ICacheVersionService, CacheVersionService>();
        return services;
    }
}
