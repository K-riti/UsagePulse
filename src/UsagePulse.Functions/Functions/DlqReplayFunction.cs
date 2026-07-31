using System.Net;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using UsagePulse.Contracts;
using UsagePulse.Functions.Configuration;
using UsagePulse.Serialization;

namespace UsagePulse.Functions.Functions;

public sealed class DlqReplayFunction : IAsyncDisposable
{
    private readonly ServiceBusReceiver receiver;
    private readonly ServiceBusSender sender;
    private readonly ILogger<DlqReplayFunction> logger;

    public DlqReplayFunction(ServiceBusClient serviceBusClient, UsagePulseSettings settings, ILogger<DlqReplayFunction> logger)
    {
        receiver = serviceBusClient.CreateReceiver(settings.DeadLetterQueue);
        sender = serviceBusClient.CreateSender(settings.ServiceBusQueue);
        this.logger = logger;
    }

    [Function(nameof(DlqReplayFunction))]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "operations/dlq/replay")] HttpRequestData request,
        CancellationToken cancellationToken)
    {
        var replayRequest = await request.ReadFromJsonAsync<DlqReplayRequest>(cancellationToken) ?? new DlqReplayRequest();
        var maxMessages = Math.Clamp(replayRequest.MaxMessages, 1, 200);
        var messages = await receiver.ReceiveMessagesAsync(maxMessages, TimeSpan.FromSeconds(5), cancellationToken);

        var received = messages.Count;
        var replayed = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var message in messages)
        {
            try
            {
                var envelope = DeadLetterEnvelopeSerializer.Deserialize(message.Body.ToString());
                if (envelope is null)
                {
                    await receiver.DeadLetterMessageAsync(message, DeadLetterReasonCode.ReplayFailed.ToString(), "Unable to deserialize dead-letter envelope.", cancellationToken);
                    failed++;
                    continue;
                }

                if (!MatchesFilter(envelope, replayRequest))
                {
                    await receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                    skipped++;
                    continue;
                }

                if (replayRequest.DryRun)
                {
                    await receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                    skipped++;
                    continue;
                }

                var replayMessage = new ServiceBusMessage(UsageEventJsonSerializer.Serialize(envelope.UsageEvent))
                {
                    MessageId = envelope.UsageEvent.EventId,
                    Subject = "usage-event-replay",
                    SessionId = envelope.UsageEvent.TenantId
                };

                replayMessage.ApplicationProperties["replayedFromDlq"] = true;
                replayMessage.ApplicationProperties["originalFailureReason"] = envelope.Failure.ReasonCode.ToString();
                replayMessage.ApplicationProperties["replayedAtUtc"] = DateTimeOffset.UtcNow.ToString("O");

                await sender.SendMessageAsync(replayMessage, cancellationToken);
                await receiver.CompleteMessageAsync(message, cancellationToken);
                replayed++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to replay dead-lettered event. MessageId={MessageId}", message.MessageId);
                await receiver.AbandonMessageAsync(message, cancellationToken: cancellationToken);
                failed++;
            }
        }

        var response = request.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new DlqReplayResult(received, replayed, skipped, failed), cancellationToken);
        return response;
    }

    private static bool MatchesFilter(DeadLetterEnvelope envelope, DlqReplayRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.TenantId) && !string.Equals(envelope.UsageEvent.TenantId, request.TenantId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(request.ReasonCode) && !string.Equals(envelope.Failure.ReasonCode.ToString(), request.ReasonCode, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    public async ValueTask DisposeAsync()
    {
        await receiver.DisposeAsync();
        await sender.DisposeAsync();
    }
}

public sealed record DlqReplayRequest(int MaxMessages = 50, string? TenantId = null, string? ReasonCode = null, bool DryRun = false);

public sealed record DlqReplayResult(int Received, int Replayed, int Skipped, int Failed);
