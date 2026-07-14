namespace OrizonAgents.Workers;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Worker(ILogger<Worker> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<OrizonAgents.Application.Billing.IBillingCycleProcessor>();
            int processed = await processor.ProcessAsync(DateTime.UtcNow, stoppingToken);
            var whatsAppProcessor = scope.ServiceProvider.GetRequiredService<OrizonAgents.Application.WhatsApp.IWhatsAppProcessor>();
            var inbox = await whatsAppProcessor.ProcessInboxAsync(stoppingToken);
            var outbox = await whatsAppProcessor.ProcessOutboxAsync(stoppingToken);
            int pruned = await whatsAppProcessor.PruneInboxPayloadsAsync(stoppingToken);
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Billing cycle processed {SubscriptionCount} subscriptions at {Time}", processed, DateTimeOffset.UtcNow);
                _logger.LogInformation("WhatsApp processor inbox {InboxProcessed}/{InboxFailed}, outbox {OutboxProcessed}/{OutboxFailed}, pruned {PrunedPayloads}", inbox.Processed, inbox.Failed, outbox.Processed, outbox.Failed, pruned);
            }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
