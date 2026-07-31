# UsagePulse

UsagePulse is a cloud-scale Azure usage analytics platform for near real-time telemetry ingestion, processing, analytics, and query.

## Tech Stack

- .NET 8
- Azure Functions (isolated worker)
- Azure Event Hubs
- Azure Service Bus
- Azure Cosmos DB
- Azure Data Explorer (Kusto)
- Azure Stream Analytics
- AKS
- Terraform
- Azure DevOps
- Azure Monitor + Application Insights + OpenTelemetry

## Current Capabilities

- Event-driven ingestion from Event Hubs to Service Bus.
- Queue-based processing pipeline with:
  - retry with exponential backoff
  - idempotency checks
  - application-level dead-letter publishing
  - payload validation guardrails (required fields, positive quantity, valid timestamp)
- Persistence in Cosmos DB for raw usage events.
- Kusto sink integration point for analytics export.
- Query API endpoint for tenant usage summary.
- Terraform baseline for core Azure resources.
- Azure DevOps pipeline for build, test, and infrastructure plan/apply.

## Why This Project Matters (Impact)

Without a platform like UsagePulse, product teams usually face these problems:

- No trusted usage data for billing, adoption, and KPI decisions.
- Lost or duplicated telemetry during traffic spikes.
- Slow incident debugging due to missing distributed traces.
- Incorrect analytics from malformed events or wrong aggregations.

UsagePulse addresses those directly by providing resilient ingestion, validation, idempotent processing, and observable distributed workflows.

### Impact Summary

- **Business:** gives reliable usage data for billing, adoption, and product decisions.
- **Engineering:** prevents bad telemetry from corrupting analytics.
- **Operations:** improves trust in near real-time dashboards and incident analysis.

## Problem Fixed in This Iteration

Two concrete reliability/data-quality issues were addressed:

1. Invalid events could enter processing and pollute analytics.  
   Fix: Early validation in `UsageEventProcessor` now rejects malformed payloads and dead-letters them immediately.

2. Feature breakdown could under/overstate usage by counting events instead of summing quantity.  
   Fix: `UsageSummaryService` now aggregates per-feature totals using `quantity`.

## Solution Structure

- `src/UsagePulse.Contracts`  
  Shared domain contracts (`UsageEvent`, `ProcessingResult`, `TenantUsageSummary`).
- `src/UsagePulse.Processing`  
  Pipeline abstractions and `UsageEventProcessor` implementation.
- `src/UsagePulse.Functions`  
  Ingestion and processing Azure Functions + infrastructure adapters.
- `src/UsagePulse.QueryApi`  
  API for tenant usage summaries (AKS target).
- `tests/UsagePulse.Processing.Tests`  
  Unit tests for duplicate handling, retry behavior, and dead-letter flow.
- `infra/terraform`  
  IaC for resource group, Event Hubs, Service Bus, Cosmos DB, ADX, Stream Analytics, AKS, and monitoring.
- `deploy/aks`  
  Kubernetes deployment + service + HPA manifest.
- `deploy/stream-analytics`  
  Stream Analytics query template.
- `azure-pipelines.yml`  
  CI/CD pipeline definition.

## High-Level Flow

1. Producers publish usage events to Event Hubs.
2. `UsageIngestionFunction` validates and forwards events to Service Bus work queue.
3. `UsageProcessingFunction` consumes queue messages.
4. `UsageEventProcessor` applies idempotency + retry and writes to data stores/sinks.
5. Failed events are published to dead-letter queue.
6. Query API reads from Cosmos DB and returns usage summaries.
7. Telemetry is exported via OpenTelemetry to Azure Monitor/App Insights.

## API Endpoints

- `GET /health`
- `GET /api/usage/{tenantId}/summary?from=<iso>&to=<iso>`

## Local Development

### Prerequisites

- .NET SDK 8.x
- Azure Functions Core Tools (for local Functions run)
- Terraform >= 1.6

### Build & Test

```bash
dotnet restore UsagePulse.slnx
dotnet build UsagePulse.slnx
dotnet test UsagePulse.slnx
```

### Run Query API

```bash
dotnet run --project src/UsagePulse.QueryApi/UsagePulse.QueryApi.csproj
```

### Run Functions

1. Copy `src/UsagePulse.Functions/local.settings.sample.json` to `local.settings.json`.
2. Fill connection settings.
3. Run:

```bash
func start --csharp
```

## Configuration

### Functions (`UsagePulse` section)

- `EventHubName`
- `ServiceBusQueue`
- `DeadLetterQueue`
- `ServiceBusNamespace` or `ServiceBusConnectionString`
- `CosmosEndpoint` or `CosmosConnectionString`
- `CosmosDatabase`
- `EventsContainer`
- `IdempotencyContainer`
- `KustoIngestionEndpoint`
- `MaxProcessingAttempts`
- `BaseRetryDelayMs`

### Query API (`UsagePulse` section)

- `CosmosEndpoint` or `CosmosConnectionString`
- `CosmosDatabase`
- `EventsContainer`

## Terraform

```bash
cd infra/terraform
terraform init
terraform validate
terraform plan
```

## Feature Roadmap (Next Implementations)

### High-Value Features to Take UsagePulse to the Next Level

#### P0 (Immediate Value)

1. **Schema Registry + Contract Versioning**
   - Enforce event compatibility at ingestion.
   - Prevents breaking producers from corrupting downstream analytics.

2. **DLQ Replay & Self-Healing Workflow**
   - Add replay API/job to reprocess corrected poison events.
   - Reduces data loss and manual production operations.

3. **Kusto Native Ingestion with Batching**
   - Replace generic HTTP sink with ADX ingestion SDK and buffered batches.
   - Improves throughput and lowers ingestion latency/cost.

4. **SLO-based Alerting Pack**
   - Alerts on queue lag, failed processing %, ingestion latency, and throughput drops.
   - Enables proactive operations before customer-visible impact.

#### P1 (Scale & Product Value)

5. **Tenant Quotas + Rate Limiting + Burst Control**
   - Protects shared infrastructure from noisy-neighbor tenants.
   - Supports monetization tiers and enterprise controls.

6. **Low-Latency Query Path (Materialized Views + Cache)**
   - Add pre-aggregated views for common windows (5m, 1h, 24h).
   - Makes dashboards faster and more predictable at scale.

7. **Real-Time Usage Dashboard Service**
   - Dedicated dashboard/API for usage trends, spikes, and anomalies.
   - Converts telemetry into product and customer-facing insights.

#### P2 (Enterprise Readiness)

8. **Anomaly Detection (Usage/Billing Guardrails)**
   - Detect unexpected drops/spikes and suspicious usage patterns.
   - Improves trust for finance, product, and security teams.

9. **Managed Identity + Key Vault Everywhere**
   - Remove connection-string dependency from runtime paths.
   - Improves security posture and auditability.

10. **Blue/Green + Progressive Delivery**
   - Safer releases for API and Functions with controlled rollout.
   - Minimizes deployment risk and rollback time.

## Refactoring Backlog

### Domain & Contracts

- Introduce strict value objects (`TenantId`, `EventId`, `FeatureName`).
- Add validation layer at contract boundary.

### Processing Layer

- Split `UsageEventProcessor` into small pipeline behaviors (idempotency, retry, persistence, analytics sink).
- Introduce resilience policies with Polly for standardized retry/circuit-breaker behavior.
- Make dead-letter reason codes strongly typed.

### Functions Layer

- Move trigger handlers to thin orchestrators only.
- Centralize serialization/deserialization settings.
- Add correlation propagation helpers for distributed tracing.

### Query API

- Separate query models from storage models.
- Add pagination and feature-level filtering.
- Add caching layer for common summary windows.

### Cross-Cutting

- Add analyzers and enforce code style in CI.
- Add architecture tests to keep layering boundaries.
- Improve observability conventions (metric names, trace attributes, log schema).
- Extract environment-specific configuration into dedicated deployment overlays.

## Resume Alignment

This project demonstrates the architecture described in your resume:

- Distributed event-driven ingestion and processing with Event Hubs + Service Bus + Functions.
- Fault-tolerant data pipeline with retries, idempotency, and dead-letter handling.
- Analytics storage/serving through Cosmos DB, Kusto, and Stream Analytics.
- Cloud automation and operational readiness using Terraform, Azure DevOps, AKS, and OpenTelemetry.
