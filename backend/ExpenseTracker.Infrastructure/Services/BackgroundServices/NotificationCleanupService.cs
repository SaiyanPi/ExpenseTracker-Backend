using ExpenseTracker.Application.Common.Retention;
using ExpenseTracker.Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace ExpenseTracker.Infrastructure.Services.BackgroundServices;

public class NotificationCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly NotificationRetentionOptions _retentionOptions;

    public NotificationCleanupService(
        IServiceScopeFactory serviceScopeFactory,
        IOptions<NotificationRetentionOptions> retentionOptions)
    {
        _scopeFactory = serviceScopeFactory;
        _retentionOptions = retentionOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();

            var repo = scope.ServiceProvider
                .GetRequiredService<INotificationRepository>();

            var cutoffDate = DateTime.UtcNow
                .AddDays(-_retentionOptions.RetentionDays);

            await repo.DeleteOlderThanAsync(
                cutoffDate,
                stoppingToken);

            await Task.Delay(
                TimeSpan.FromHours(_retentionOptions.CleanupIntervalHours),
                stoppingToken);
        }
    }
}