using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UsagePulse.Contracts;
using UsagePulse.Processing.Abstractions;
using UsagePulse.Processing.Options;
using UsagePulse.Processing.Services;

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

    private static UsageEventProcessor CreateProcessor(
        IIdempotencyStore idempotencyStore,
        IUsageEventRepository? repository = null,
        IUsageAnalyticsSink? analyticsSink = null,
        IDeadLetterSink? deadLetterSink = null)
    {
        return new UsageEventProcessor(
            idempotencyStore,
            repository ?? new FakeUsageRepository(),
            analyticsSink ?? new FakeAnalyticsSink(),
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

        public Task StoreAsync(UsageEvent usageEvent, CancellationToken cancellationToken)
        {
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
        public List<string> Failures { get; } = [];

        public Task PublishAsync(UsageEvent usageEvent, string reason, CancellationToken cancellationToken)
        {
            Failures.Add(reason);
            return Task.CompletedTask;
        }
    }
}
