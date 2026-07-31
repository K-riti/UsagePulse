# UsagePulse

UsagePulse is a .NET 8 reference implementation for near real-time usage event ingestion, resilient processing, and tenant-facing usage read models on Azure.

The repository is intentionally documented around what is implemented today, not the full platform vision.

## Business outcomes

UsagePulse is meant to improve four outcomes that matter to product and platform teams:

- **Reliability:** reject malformed or incompatible events before they pollute downstream data.
- **Recovery:** make poison-message handling and replay operationally manageable.
- **Analytics accuracy:** keep hot summary views aligned with accepted events.
- **Scalability foundation:** provide queue-based processing, batching, and tenant-aware throttling as a base for future growth.

## What is implemented today

### Ingestion and contract validation

- Event Hub-triggered ingestion through `UsageIngestionFunction`.
- Version-aware contract validation at ingestion time.
  - Uses `SchemaVersion`, `Source`, and per-rule compatibility settings.
  - This is **not** a separate schema registry product; it is in-process contract validation logic.
- Tenant quota and burst checks before work is sent to the Service Bus processing queue.
- Correlation propagation across queue boundaries.

### Processing pipeline

- Thin Functions triggers with orchestration separated from processing logic.
- Pipeline-style event processor with stages for:
  - validation
  - deduplication
  - persistence
  - analytics export
  - finalization
- Validation failures are dead-lettered.
- Duplicate events are skipped through idempotency checks.
- Retry and circuit-breaker behavior is implemented with Polly.
- Dead-letter reasons and validation codes are strongly typed.

### Storage and read models

- Raw event persistence in Cosmos DB.
- Hot summary documents in Cosmos DB for dashboard-style queries.
- Summary windows implemented today:
  - `5m`
  - `1h`
  - `24h`
- Realtime dashboard endpoint backed by those Cosmos summary documents.

### Analytics export and recovery

- Buffered Azure Data Explorer ingestion via the Kusto ingestion SDK.
- Configurable batch size and flush interval.
- Dead-letter replay endpoint with:
  - `MaxMessages`
  - tenant filtering
  - reason-code filtering
  - dry-run mode

### Security and operations foundation

- Managed Identity-first configuration.
- Optional Azure Key Vault bootstrap.
- OpenTelemetry instrumentation hooks.
- Architecture tests that protect current layering rules.

## What is not implemented yet

The following items should be treated as roadmap work, not current platform guarantees:

- A true external schema registry.
- A standardized event envelope with explicit metadata/payload separation.
- An outbox pattern between Cosmos DB persistence and ADX/Kusto export.
- Fully separated hot and cold processing paths.
- Published end-to-end load benchmarks with events/sec and P95 latency proof.
- SLO-driven alert packs and automated remediation.
- Anomaly detection.
- Blue/green or progressive delivery.
- A stricter `Domain -> Application -> Infrastructure -> Functions/API` architecture split.

## Current architecture

```text
Event Hubs
  -> UsageIngestionFunction
  -> ingress policy evaluation
  -> Service Bus work queue
  -> UsageProcessingFunction
  -> processing pipeline
     -> Cosmos DB raw event store
     -> Cosmos DB summary view updates
     -> buffered ADX/Kusto export
     -> dead-letter queue on failure

Query API
  -> Cosmos DB summary views for dashboard windows
  -> Cosmos DB raw events for broader summary queries
```

## Hot path vs cold path

The repository already has the beginnings of hot/cold separation, but it is not fully decoupled yet.

- **Hot path today:** Cosmos DB summary view documents are updated during event processing and serve the realtime dashboard endpoint.
- **Cold path today:** the same processing flow also queues accepted events for batched ADX/Kusto ingestion.
- **Important limitation:** both paths are still initiated from the same processing transaction flow, so there is no outbox yet to guarantee atomic handoff between persistence and analytics export.

## Summary ownership

Materialized summaries are updated **inline during event processing**, not by a separate aggregation job.

Specifically:
- the processing pipeline persists accepted usage events
- the analytics sink updates Cosmos summary view documents for `5m`, `1h`, and `24h` windows
- that same sink also batches events for ADX/Kusto ingestion

This design keeps dashboards simple and low-latency, but it also means summary maintenance currently shares the event-processing path.

## Event contract today

The current event model is `UsageEvent`:

- `EventId`
- `TenantId`
- `UserId`
- `Feature`
- `Quantity`
- `OccurredAt`
- `Dimensions`
- `SchemaVersion`
- `Source`

The repo also uses strict value objects for:
- `EventId`
- `TenantId`
- `FeatureName`

### Planned envelope evolution

A future revision should move to a standardized envelope such as:

- `correlationId`
- `tenantId`
- `schemaVersion`
- `eventType`
- `occurredAt`
- `source`
- `payload`

That envelope is **planned**, not implemented in the current codebase.

## API surface

### Query API

- `GET /health`
- `GET /api/usage/{tenantId}/summary?from=<iso>&to=<iso>`
- `GET /api/dashboard/{tenantId}/realtime?window=5m|1h|24h`

### Functions

- Event Hub-triggered ingestion via `UsageIngestionFunction`
- Service Bus-triggered processing via `UsageProcessingFunction`
- HTTP replay endpoint via `POST /api/operations/dlq/replay`

## Dashboard examples

### Example dashboard metrics

The realtime dashboard endpoint is intended to surface:

- total event count for the selected window
- total quantity for the selected window
- feature-level usage breakdown
- bucketed time-series points for trend charts

### Example realtime dashboard response

```json
{
  "tenantId": "tenant-a",
  "from": "2026-01-10T10:00:00Z",
  "to": "2026-01-10T11:00:00Z",
  "eventCount": 128,
  "totalQuantity": 742,
  "featureBreakdown": {
    "dashboard": 420,
    "export": 210,
    "alerts": 112
  },
  "points": [
    {
      "bucketStart": "2026-01-10T10:00:00Z",
      "bucketEnd": "2026-01-10T10:05:00Z",
      "eventCount": 11,
      "totalQuantity": 57
    },
    {
      "bucketStart": "2026-01-10T10:05:00Z",
      "bucketEnd": "2026-01-10T10:10:00Z",
      "eventCount": 14,
      "totalQuantity": 61
    }
  ]
}
```

### Example summary response

```json
{
  "tenantId": "tenant-a",
  "from": "2026-01-09T11:00:00Z",
  "to": "2026-01-10T11:00:00Z",
  "eventCount": 2210,
  "totalQuantity": 13840,
  "featureBreakdown": {
    "dashboard": 6400,
    "export": 5030,
    "alerts": 2410
  }
}
```

## Performance and proof

### Verified in this repository

The following items are currently backed by code and automated validation in the repo:

- solution builds successfully on .NET 8
- processing, Functions, and architecture tests pass
- retry, duplicate suppression, schema rejection, quota rejection, and dead-letter serialization are covered by tests
- batched ADX/Kusto export is implemented with default settings of:
  - `KustoBatchSize = 250`
  - `KustoFlushIntervalSeconds = 5`
- replay requests are capped by code to a maximum of `200` messages per call

### Not yet published as proof

This repository does **not** yet include a repeatable benchmark harness or production evidence for:

- sustained events/sec
- P95 or P99 ingestion latency
- queue lag under load
- end-to-end recovery time after downstream failures

Until those measurements are added, the README intentionally avoids claiming production-scale throughput numbers.

## Target SLOs

The following targets are recommended and documented here as goals, not current guarantees:

| Area | Target |
| --- | --- |
| API availability | 99.9% monthly |
| Accepted event processing success rate | >= 99.95% |
| Queue lag | < 60 seconds at steady state |
| P95 ingestion-to-summary latency | < 30 seconds |
| DLQ replay recovery for common operator cases | < 15 minutes |

## Solution layout

- `src/UsagePulse.Contracts`  
  Shared contracts, typed identifiers, failures, and response models.
- `src/UsagePulse.Serialization`  
  JSON serialization helpers for usage events and dead-letter envelopes.
- `src/UsagePulse.Processing`  
  Processing abstractions, pipeline stages, telemetry, and resilience behavior.
- `src/UsagePulse.Functions`  
  Azure Functions host, orchestrators, triggers, replay endpoint, and infrastructure adapters.
- `src/UsagePulse.QueryApi`  
  Read API for summaries and realtime dashboard views.
- `tests/UsagePulse.Processing.Tests`  
  Unit tests for retries, dead-letter behavior, duplicates, and validation.
- `tests/UsagePulse.Functions.Tests`  
  Unit tests for ingress policy decisions and dead-letter envelope behavior.
- `tests/UsagePulse.Architecture.Tests`  
  Tests for current layering boundaries.

## Architecture posture

Current enforced boundaries are modest:

- contracts must not reference upper layers
- processing must not reference Functions or Query API adapters

The intended next step is to evolve toward clearer `Domain -> Application -> Infrastructure -> Functions/API` boundaries. That stronger layering is planned work, not a completed claim.

## Operations and decision records

- Runbooks: `docs/runbooks/operations.md`
- ADRs: `docs/adr/README.md`

## Configuration

Both the Functions app and Query API use the `UsagePulse` configuration section.

### Core messaging and storage

- `EventHubName`
- `ServiceBusQueue`
- `DeadLetterQueue`
- `ServiceBusNamespace`
- `ServiceBusConnectionString`
- `CosmosEndpoint`
- `CosmosConnectionString`
- `CosmosDatabase`
- `EventsContainer`
- `IdempotencyContainer`
- `SummaryViewsContainer`

### Analytics export

- `KustoClusterUri`
- `KustoDatabase`
- `KustoTable`
- `KustoManagedIdentityClientId`
- `KustoBatchSize`
- `KustoFlushIntervalSeconds`

### Contract and tenant controls

- `CurrentSchemaVersion`
- `MinimumCompatibleSchemaVersion`
- `SchemaContracts`
- `DefaultTenantQuota`
- `TenantQuotas`

### Processing resilience

- `MaxProcessingAttempts`
- `BaseRetryDelayMs`
- `CircuitBreakerSamplingSeconds`
- `CircuitBreakerDurationSeconds`
- `CircuitBreakerMinimumThroughput`
- `CircuitBreakerFailureRatio`

### Security and identity

- `KeyVaultUri`
- `AllowConnectionStringFallback`

Managed Identity is the default runtime path. Connection strings are supported only as an explicit fallback.

## Local development

### Prerequisites

- .NET SDK 8.x
- Azure Functions Core Tools
- Terraform 1.6 or later
- Access to Azure resources for Cosmos DB, Service Bus, Event Hubs, and optional ADX

### Build

```bash
dotnet restore UsagePulse.slnx
dotnet build UsagePulse.slnx
```

### Test

```bash
dotnet test UsagePulse.slnx
dotnet test tests/UsagePulse.Functions.Tests/UsagePulse.Functions.Tests.csproj
```

### Run the Query API

```bash
dotnet run --project src/UsagePulse.QueryApi/UsagePulse.QueryApi.csproj
```

### Run the Functions app

1. Copy `src/UsagePulse.Functions/local.settings.sample.json` to `local.settings.json`.
2. Populate the `UsagePulse` configuration values.
3. Start the Functions host:

```bash
func start --csharp
```

## Roadmap

Near-term priorities:

1. Add an outbox pattern between persistence and analytics export.
2. Split hot dashboard updates from long-term analytics export more cleanly.
3. Introduce a standardized event envelope.
4. Add load benchmarks and publish events/sec and latency evidence.
5. Add alerting, anomaly detection, and delivery safety controls.

## Infrastructure

### Terraform

```bash
cd infra/terraform
terraform init
terraform validate
terraform plan
```

### Deployment assets

- `deploy/aks/usagepulse-queryapi.yaml`
- `deploy/stream-analytics/usagepulse-job.asaql`
