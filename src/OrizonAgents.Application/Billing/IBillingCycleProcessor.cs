namespace OrizonAgents.Application.Billing;

public interface IBillingCycleProcessor
{
    Task<int> ProcessAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
