using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrizonAgents.Application.Agents.Execution.Models;
using OrizonAgents.Application.Agents.Execution.Telemetry;
using OrizonAgents.Domain.Agents;
using OrizonAgents.Infrastructure.Persistence;

namespace OrizonAgents.Infrastructure.Agents.Execution.Telemetry;

public sealed class AgentExecutionTelemetry(
    IDbContextFactory<OrizonAgentsDbContext> contextFactory,
    TimeProvider timeProvider,
    ILogger<AgentExecutionTelemetry> logger)
    : IAgentExecutionTelemetry
{
    public IAgentExecutionTelemetrySession Start(
        AgentExecutionTelemetryStart start) =>
        new Session(contextFactory, timeProvider, logger, start);

    private sealed class Session : IAgentExecutionTelemetrySession
    {
        private readonly IDbContextFactory<OrizonAgentsDbContext> _contextFactory;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger _logger;
        private readonly AgentExecutionTelemetryStart _start;
        private readonly DateTime _startedAtUtc;
        private readonly long _startedTimestamp;
        private int _modelCalls;
        private int _toolExecutions;
        private int _toolSuccesses;
        private int _toolFailures;
        private int _toolApprovals;
        private int _ragResults;
        private int _toolContextCharacters;
        private int _contextReductionCharacters;
        private long? _inputTokens;
        private long? _outputTokens;
        private long? _totalTokens;
        private bool _completed;

        public Session(
            IDbContextFactory<OrizonAgentsDbContext> contextFactory,
            TimeProvider timeProvider,
            ILogger logger,
            AgentExecutionTelemetryStart start)
        {
            _contextFactory = contextFactory;
            _timeProvider = timeProvider;
            _logger = logger;
            _start = start;
            _startedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
            _startedTimestamp = timeProvider.GetTimestamp();
        }

        public void RecordModelCall() => _modelCalls++;

        public void RecordModelUsage(AiChatUsage? usage)
        {
            if (usage is null)
            {
                return;
            }

            _inputTokens = Add(_inputTokens, usage.InputTokens);
            _outputTokens = Add(_outputTokens, usage.OutputTokens);
            _totalTokens = Add(_totalTokens, usage.TotalTokens);
        }

        public void RecordToolExecution(bool succeeded, bool approvalRequired)
        {
            _toolExecutions++;
            _toolSuccesses += succeeded ? 1 : 0;
            _toolApprovals += approvalRequired ? 1 : 0;
            _toolFailures += !succeeded && !approvalRequired ? 1 : 0;
        }

        public void RecordRagResults(int count) =>
            _ragResults += Math.Max(0, count);

        public void RecordToolContext(int originalCharacters, int usedCharacters)
        {
            int safeOriginal = Math.Max(0, originalCharacters);
            int safeUsed = Math.Max(0, usedCharacters);
            _toolContextCharacters += safeUsed;
            _contextReductionCharacters += Math.Max(0, safeOriginal - safeUsed);
        }

        public async Task CompleteAsync(
            Guid? conversationId,
            bool succeeded,
            CancellationToken cancellationToken = default)
        {
            if (_completed)
            {
                return;
            }

            _completed = true;
            DateTime completedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            long durationMs = (long)_timeProvider
                .GetElapsedTime(_startedTimestamp)
                .TotalMilliseconds;

            var usage = new AgentExecutionUsage(
                _start.TenantId,
                _start.AgentId,
                conversationId,
                _start.Provider,
                _start.Model,
                _startedAtUtc,
                completedAtUtc,
                durationMs,
                succeeded,
                _modelCalls,
                _toolExecutions,
                _toolSuccesses,
                _toolFailures,
                _toolApprovals,
                _ragResults,
                _toolContextCharacters,
                _contextReductionCharacters,
                _inputTokens,
                _outputTokens,
                _totalTokens);

            try
            {
                await using OrizonAgentsDbContext context =
                    await _contextFactory.CreateDbContextAsync(cancellationToken);
                context.AgentExecutionUsages.Add(usage);
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    "Falha ao persistir telemetria da execução do agente {AgentId}. Tipo: {ExceptionType}.",
                    _start.AgentId,
                    exception.GetType().Name);
            }
        }

        private static long? Add(long? current, long? value) =>
            value.HasValue ? (current ?? 0) + value.Value : current;
    }
}
