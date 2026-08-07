# Operations Runbook

## Scope

This runbook covers the current operational surface that exists in the repository:

- dead-letter handling
- DLQ replay
- Azure Monitor and Application Insights alert triage
- Stream Analytics pipeline checks
- current limitations

## Dead-letter triage

Check for these failure categories first:

1. `InvalidPayload`
   - The queue message body could not be deserialized.
   - Likely causes: malformed JSON, incompatible payload shape, producer bug.

2. `NullPayload`
   - The message deserialized to null.
   - Likely causes: empty message body or invalid producer serialization.

3. `ValidationFailed`
   - The event contract was syntactically valid but failed business validation.
   - Examples: missing `UserId`, non-positive `Quantity`, invalid `OccurredAt`.

4. `SchemaIncompatible`
   - The event `SchemaVersion` or source/feature compatibility rule was rejected.
   - Check `CurrentSchemaVersion`, `MinimumCompatibleSchemaVersion`, and `SchemaContracts`.

5. `QuotaExceeded`
   - The tenant exceeded the configured request or burst window.
   - Check `DefaultTenantQuota` and any tenant overrides.

6. `ProcessingFailed` or `CircuitOpen`
   - The event was accepted but downstream processing or resilience protection failed.
   - Check Cosmos DB connectivity, Kusto configuration, and recent deployment changes.

## Replay procedure

Current replay endpoint:

- `POST /api/operations/dlq/replay`

Supported request fields:

- `maxMessages`
- `tenantId`
- `reasonCode`
- `dryRun`

### Safe replay sequence

1. Identify the dominant dead-letter reason.
2. Fix the underlying issue first.
3. Start with a dry run.
4. Replay a small filtered batch.
5. Confirm processing and query results.
6. Increase replay batch size gradually.

### Example replay request

```json
{
  "maxMessages": 25,
  "tenantId": "tenant-a",
  "reasonCode": "SchemaIncompatible",
  "dryRun": true
}
```

### Example replay result

```json
{
  "received": 25,
  "replayed": 0,
  "skipped": 25,
  "failed": 0
}
```

## Incident recovery checklist

### If ingestion drops unexpectedly

- Check Event Hubs input health.
- Check Function host status.
- Check whether quota rules are rejecting a tenant unexpectedly.
- Check dead-letter growth for `SchemaIncompatible` or `ValidationFailed` spikes.

### If dashboard numbers lag

- Check Service Bus queue depth and consumer health.
- Check Cosmos DB write health.
- Verify summary view updates are succeeding.
- Remember that summary updates are maintained inline during event processing.

### If long-term analytics lag

- Check ADX/Kusto cluster connectivity and identity configuration.
- Check whether `KustoClusterUri` is configured.
- Review batch settings: `KustoBatchSize`, `KustoFlushIntervalSeconds`.
- Note that there is no outbox pattern yet, so recovery may require targeted replay.

## Alert triage

Terraform provisions the following starter alert rules:

- Service Bus work queue depth (`ActiveMessages`)
- Event Hubs ingress volume (`IncomingMessages`)

When an alert fires:

1. Validate whether it is a sustained signal over at least one full alert window.
2. Check correlated traces and dependency failures in Application Insights.
3. Confirm queue growth trend and processor throughput.
4. Apply replay only after root cause is corrected.

## Stream Analytics checks

For the Stream Analytics job:

- verify the job is running
- verify `EventHubInput` and `CosmosAggregateOutput` are healthy
- verify transformation query deployment matches `deploy/stream-analytics/usagepulse-job.asaql`

If the job drifts, redeploy using:

- `deploy/stream-analytics/deploy-stream-analytics.ps1`

## Current operational limitations

- No outbox pattern currently guarantees atomic handoff from event persistence to analytics export.
- Hot and cold paths are only partially separated.
- Automated remediation is not implemented yet.
