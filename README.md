# UsagePulse

UsagePulse is a near real-time Azure usage telemetry platform for ingesting product events, enforcing contract safety, processing events reliably, exporting analytics, and serving tenant-facing usage summaries.

## What the platform does

UsagePulse is designed for teams that need trusted usage data for analytics, product intelligence, and billing-adjacent reporting. The platform focuses on four core guarantees:

- **Safe ingestion** with schema compatibility checks and tenant-aware throttling.
- **Reliable processing** with validation, idempotency, retries, circuit breaking, and dead-letter handling.
- **Fast analytics delivery** through Cosmos-backed summaries and batched Azure Data Explorer ingestion.
- **Operational recovery** with a replay endpoint for dead-lettered events.

## Current architecture

```text
Event Hubs -> UsageIngestionFunction -> Service Bus work queue -> UsageProcessingFunction
          -> ingress policy checks                         -> processing pipeline
                                                           -> Cosmos DB raw event store
                                                           -> Cosmos DB summary view store
                                                           -> Azure Data Explorer batched ingest
                                                           -> dead-letter queue + replay workflow

Query API -> Cosmos DB summaries/raw events -> tenant summary + realtime dashboard endpoints
```

## Tech stack

- .NET 8
- Azure Functions isolated worker
- Azure Event Hubs
- Azure Service Bus
- Azure Cosmos DB
- Azure Data Explorer (Kusto)
- OpenTelemetry + Azure Monitor / Application Insights
- Terraform
- Azure DevOps
- AKS deployment manifests for the query API

## Implemented capabilities

### Ingestion and contract safety

- Event ingestion from Event Hubs into Azure Functions.
- Strict contract model with value objects:
  - `EventId`
  - `TenantId`
  - `FeatureName`
- Version-aware event contract through `UsageEvent.SchemaVersion` and `UsageEvent.Source`.
- Ingress-time schema compatibility enforcement:
  - minimum and current supported schema versions
  - optional per-source and per-feature contract rules
- Tenant quota enforcement with burst control before events enter the main processing queue.
- Correlation propagation across asynchronous boundaries for distributed tracing.

### Processing pipeline

- Thin trigger handlers with orchestration separated from business logic.
- Pipeline-style processor with dedicated stages for:
  - validation
  - deduplication
  - persistence
  - analytics export
  - finalization
- Validation at the contract boundary for malformed or incomplete usage events.
- Idempotency protection to prevent duplicate event processing.
- Retry with exponential backoff.
- Circuit breaker protection using Polly.
- Strongly typed dead-letter reason codes and validation codes.

### Storage and analytics

- Raw usage event persistence in Cosmos DB.
- Materialized summary view documents in Cosmos DB for hot dashboard windows.
- Native Azure Data Explorer queued ingestion using the Kusto ingestion SDK.
- Buffered batching for analytics export with configurable batch size and flush interval.
- Summary windows currently maintained for:
  - `5m`
  - `1h`
  - `24h`

### Recovery and operations

- Dead-letter publishing for invalid, incompatible, quota-exceeded, and failed events.
- Replay HTTP function for dead-letter queue recovery with:
  - max message limits
  - tenant filter
  - reason-code filter
  - dry-run mode
- Managed Identity-first runtime configuration.
- Azure Key Vault integration for configuration bootstrapping.
- OpenTelemetry instrumentation for processing metrics and traces.

### Query experience

- Tenant usage summary endpoint.
- Realtime dashboard endpoint backed by materialized summary windows.
- Swagger enabled in development for the query API.

## Solution layout

- `src/UsagePulse.Contracts`  
  Shared contracts, typed identifiers, validation failures, and dashboard/summary response models.
- `src/UsagePulse.Serialization`  
  JSON serialization helpers for usage events and dead-letter envelopes.
- `src/UsagePulse.Processing`  
  Core processing abstractions, pipeline stages, telemetry, and resilience behavior.
- `src/UsagePulse.Functions`  
  Azure Functions host, ingestion/processing/replay functions, orchestrators, and infrastructure adapters.
- `src/UsagePulse.QueryApi`  
  Read API for summaries and realtime dashboard views.
- `tests/UsagePulse.Processing.Tests`  
  Unit tests for retries, dead-letter behavior, duplicates, and validation.
- `tests/UsagePulse.Functions.Tests`  
  Unit tests for ingress policy decisions and dead-letter envelope behavior.
- `tests/UsagePulse.Architecture.Tests`  
  Layering tests that guard contracts and processing boundaries.
- `infra/terraform`  
  Infrastructure as code for the Azure resource baseline.
- `deploy/aks`  
  Kubernetes manifest for the query API deployment.
- `deploy/stream-analytics`  
  Stream Analytics query template.
- `azure-pipelines.yml`  
  CI/CD pipeline definition.

## Event contract

The central event contract is `UsageEvent`:

- `EventId`
- `TenantId`
- `UserId`
- `Feature`
- `Quantity`
- `OccurredAt`
- `Dimensions`
- `SchemaVersion`
- `Source`

This model is validated both structurally and operationally before deep processing begins.

## API surface

### Query API

- `GET /health`
- `GET /api/usage/{tenantId}/summary?from=<iso>&to=<iso>`
- `GET /api/dashboard/{tenantId}/realtime?window=5m|1h|24h`

### Functions

- Event Hub-triggered ingestion via `UsageIngestionFunction`
- Service Bus-triggered processing via `UsageProcessingFunction`
- HTTP replay endpoint via `POST /api/operations/dlq/replay`

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

## Operational focus areas

The current implementation already includes foundational work for several high-value platform capabilities:

- schema compatibility checks at ingestion
- DLQ replay workflow
- native Kusto ingestion with batching
- tenant quotas and burst handling
- low-latency dashboard windows
- realtime dashboard API
- managed identity and Key Vault-first configuration
- architecture tests for layering

Remaining platform work is mainly around deeper production hardening, richer alerting, anomaly detection, progressive delivery, and broader query-path optimizations.

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

## Testing strategy

The repository currently contains:

- processing unit tests
- functions unit tests
- architecture boundary tests

These tests cover reliability-sensitive areas such as retries, duplicates, validation failures, schema rejection, quota enforcement, and basic layering rules.

## Why UsagePulse matters

Usage telemetry systems fail when they accept incompatible contracts, process duplicates during spikes, or make recovery too manual after poison messages. UsagePulse addresses those failure modes directly by combining contract checks, tenant controls, resilient processing, dead-letter replay, and fast read models in one platform.
