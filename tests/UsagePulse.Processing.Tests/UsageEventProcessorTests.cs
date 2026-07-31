using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Processing.Options;
using UsagePulse.Processing.Services;
using UsagePulse.Processing.Services.Pipeline;

namespace UsagePulse.Processing.Tests;

public class UsageEventProcessorTests
{
    [Fact]
    public async Task ProcessAsync_ReturnsDuplicate_WhenEventAlreadyHandled()
    {
        var processor = CreateProcessor(new FakeIdempotencyStore(canStart: false));
        var result = await processor.ProcessAsync(TestEvent(), CancellationToken.None);
        Assert.True(result.IsDuplicate);
    }

    [Fact]
    public async Task ProcessAsync_RetriesAndSucceeds_WhenFirstAttemptFails()
    {
        var repository = new FakeUsageRepository(failuresBeforeSuccess: 1);
        var processor = CreateProcessor(new FakeIdempotencyStore(canStart: true), repository: repository);

        var result = await processor.ProcessAsync(TestEvent(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Attempts);
    }

    [Fact]
    public async Task ProcessAsync_PublishesDeadLetter_WhenAllAttemptsFail()
    {
        var deadLetter = new FakeDeadLetterSink();
        var processor = CreateProcessor(
            new FakeIdempotencyStore(canStart: true),
            repository: new FakeUsageRepository(failuresBeforeSuccess: 10),
            deadLetterSink: deadLetter);

        var result = await processor.ProcessAsync(TestEvent(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Single(deadLetter.Failures);
    }

    [Fact]
    public async Task ProcessAsync_RejectsInvalidEvent_WithoutCallingRepository()
    {
        var repository = new FakeUsageRepository();
        var deadLetter = new FakeDeadLetterSink();
        var invalidEvent = TestEvent() with { Quantity = 0 };
        var processor = CreateProcessor(
            new FakeIdempotencyStore(canStart: true),
            repository: repository,
            deadLetterSink: deadLetter);

        var result = await processor.ProcessAsync(invalidEvent, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, result.Attempts);
        Assert.Equal(0, repository.StoreCalls);
        Assert.Single(deadLetter.Failures);
        Assert.Contains("Quantity", deadLetter.Failures[0].Message);
    }

    private static UsageEventProcessor CreateProcessor(
        IIdempotencyStore idempotencyStore,
        IUsageEventRepository? repository = null,
        IUsageAnalyticsSink? analyticsSink = null,
        IDeadLetterSink? deadLetterSink = null)
    {
        var effectiveRepository = repository ?? new FakeUsageRepository();
        var effectiveAnalytics = analyticsSink ?? new FakeAnalyticsSink();

        return new UsageEventProcessor(
            new UsageEventValidationStage(),
            new UsageEventDeduplicationStage(idempotencyStore),
            new UsageEventPersistenceStage(effectiveRepository),
            new UsageEventAnalyticsStage(effectiveAnalytics),
            new UsageEventFinalizeStage(idempotencyStore),
            deadLetterSink ?? new FakeDeadLetterSink(),
            Microsoft.Extensions.Options.Options.Create(new UsagePulsePipelineOptions { MaxProcessingAttempts = 3, BaseRetryDelayMs = 1 }),
            NullLogger<UsageEventProcessor>.Instance);
    }

    private static UsageEvent TestEvent() => new(
        Guid.NewGuid().ToString("N"),
        "tenant-a",
        "user-a",
        "dashboard",
        1,
        DateTimeOffset.UtcNow);

    private sealed class FakeIdempotencyStore : IIdempotencyStore
    {
        private readonly bool canStart;

        public FakeIdempotencyStore(bool canStart)
        {
            this.canStart = canStart;
        }

        public Task<bool> TryStartProcessingAsync(string eventId, CancellationToken cancellationToken) => Task.FromResult(canStart);

        public Task MarkProcessedAsync(string eventId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeUsageRepository : IUsageEventRepository
    {
        private int remainingFailures;

        public FakeUsageRepository(int failuresBeforeSuccess = 0)
        {
            remainingFailures = failuresBeforeSuccess;
        }

        public int StoreCalls { get; private set; }

        public Task StoreAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
        {
            StoreCalls++;

            if (remainingFailures > 0)
            {
                remainingFailures--;
                throw new InvalidOperationException("Transient failure");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeAnalyticsSink : IUsageAnalyticsSink
    {
        public Task WriteAsync(UsageEvent usageEvent, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeDeadLetterSink : IDeadLetterSink
    {
        public List<ProcessingFailure> Failures { get; } = [];

        public Task PublishAsync(UsageEvent usageEvent, ProcessingFailure failure, CancellationToken cancellationToken)
        {
            Failures.Add(failure);
            return Task.CompletedTask;
        }
    }
}
