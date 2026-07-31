# UsagePulse

UsagePulse is a cloud-scale Azure usage analytics platform built with .NET 8 and event-driven patterns.

## Stack

- .NET 8
- Azure Functions (Event Hub trigger + Service Bus trigger)
- Azure Event Hubs
- Azure Service Bus
- Azure Cosmos DB
- Azure Data Explorer (Kusto)
- Azure Stream Analytics
- AKS
- Terraform
- Azure DevOps
- Azure Monitor + Application Insights + OpenTelemetry

## Solution Layout

- `src/UsagePulse.Contracts` shared domain contracts.
- `src/UsagePulse.Processing` processing pipeline with retries, idempotency, and dead-letter hooks.
- `src/UsagePulse.Functions` ingestion and processing functions.
- `src/UsagePulse.QueryApi` AKS-hosted query API over usage telemetry.
- `tests/UsagePulse.Processing.Tests` unit tests for processor reliability behavior.
- `infra/terraform` Azure infrastructure as code.
- `deploy/aks` Kubernetes deployment manifest.
- `deploy/stream-analytics` stream analytics query.
- `azure-pipelines.yml` CI/CD pipeline.

## Architecture Flow

1. Producers send usage telemetry to Event Hubs.
2. `UsageIngestionFunction` validates and forwards events to Service Bus work queue.
3. `UsageProcessingFunction` consumes queue messages and executes the resilient processing pipeline.
4. Pipeline stores events in Cosmos DB, exports analytics events to Kusto, and dead-letters failed events.
5. Stream Analytics computes near-real-time aggregates.
6. `UsagePulse.QueryApi` serves tenant usage summaries.
7. OpenTelemetry traces are exported to Azure Monitor/Application Insights.

## Local Build

```bash
dotnet restore UsagePulse.slnx
dotnet build UsagePulse.slnx
dotnet test UsagePulse.slnx
```

## Functions Local Configuration

Copy:

- `src/UsagePulse.Functions/local.settings.sample.json`

as:

- `src/UsagePulse.Functions/local.settings.json`

Then set Event Hubs, Service Bus, Cosmos DB, and optional Kusto endpoint values.

## Terraform

```bash
cd infra/terraform
terraform init
terraform validate
terraform plan
```
