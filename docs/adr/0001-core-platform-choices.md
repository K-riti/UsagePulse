# ADR 0001: Core platform choices

## Status

Accepted

## Context

UsagePulse needs to ingest usage events, absorb bursty traffic, process events safely, maintain low-latency read models, and support longer-term analytics exports.

## Decisions

### Event Hubs for ingress

Event Hubs is used as the front-door event ingestion service because it is a natural fit for high-volume append-style telemetry streams.

### Service Bus for processing handoff

Service Bus is used between ingestion and processing to:

- isolate producers from processor availability
- support controlled retries
- enable dead-letter handling and replay workflows
- provide clearer work-queue semantics than direct inline processing

### Cosmos DB for raw events and hot summaries

Cosmos DB is used for:

- raw event persistence
- idempotency tracking
- low-latency summary documents for tenant dashboard windows

This keeps the current dashboard path simple and operationally direct.

### Azure Data Explorer for long-term analytics

Azure Data Explorer is used for batched analytical ingestion because it is well suited for time-series and usage analytics workloads.

## Consequences

### Positive

- The system already has a basic hot read path and a separate analytical export target.
- Queue-based processing improves resilience and recoverability.
- DLQ replay is practical because failed work is isolated.

### Negative

- The current implementation does not yet have an outbox pattern.
- Hot and cold paths are not fully decoupled.
- Summary maintenance currently shares the event-processing flow.

## Follow-up decisions needed

Future ADRs should cover:

- standardized event envelope design
- outbox pattern adoption
- target architecture boundaries
- SLOs and alerting policy
- deployment safety strategy
