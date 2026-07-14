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
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Billing cycle processed {SubscriptionCount} subscriptions at {Time}", processed, DateTimeOffset.UtcNow);
            }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
