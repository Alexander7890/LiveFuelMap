using System.Threading.Channels;
using LiveFuelMap.BLL.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LiveFuelMap.Infrastructure.Email;

public sealed class SubscriptionNotificationQueue : ISubscriptionNotificationQueue
{
    private readonly Channel<SubscriptionNotificationJob> _channel = Channel.CreateUnbounded<SubscriptionNotificationJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public void QueueSubscriptionCreated(int userId, int subscriptionId) =>
        _channel.Writer.TryWrite(new SubscriptionNotificationJob("subscription-created", userId, subscriptionId));

    public void QueuePriceChanges(IReadOnlyList<LiveFuelMap.BLL.DTOs.PriceChangeNotificationDto> priceChanges)
    {
        if (priceChanges.Count == 0)
            return;

        _channel.Writer.TryWrite(new SubscriptionNotificationJob("price-changes", PriceChanges: priceChanges.ToList()));
    }

    public ValueTask<SubscriptionNotificationJob> DequeueAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAsync(cancellationToken);
}

public sealed class SubscriptionNotificationWorker(
    ISubscriptionNotificationQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            SubscriptionNotificationJob job;
            try
            {
                job = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                switch (job.Type)
                {
                    case "subscription-created" when job.UserId is not null && job.SubscriptionId is not null:
                        await scope.ServiceProvider
                            .GetRequiredService<ISubscriptionEmailNotifier>()
                            .NotifySubscriptionCreatedAsync(job.UserId.Value, job.SubscriptionId.Value, stoppingToken);
                        break;

                    case "price-changes" when job.PriceChanges is { Count: > 0 }:
                        await scope.ServiceProvider
                            .GetRequiredService<IPriceChangeEmailNotifier>()
                            .NotifyAsync(job.PriceChanges, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process subscription notification job {JobType}", job.Type);
            }
        }
    }
}

public sealed class SubscriptionDigestWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionDigestWorker> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider
                    .GetRequiredService<ISubscriptionEmailNotifier>()
                    .NotifyDueScheduledAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process scheduled subscription digests.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
