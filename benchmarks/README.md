# Performance baseline harness

This folder provides a repeatable load harness to publish UsagePulse throughput and latency evidence.

## Prerequisites

- k6 installed
- A running UsagePulse Query API deployment

## Run

```bash
k6 run benchmarks/k6/usagepulse-ingestion.js --summary-export benchmarks/k6/results-summary.json
```

Environment variables:

- `USAGEPULSE_BASE_URL` (required), for example `https://usagepulse-queryapi.contoso.com`
- `USAGEPULSE_TENANT_ID` (optional, default `tenant-a`)

The current script drives `GET /api/dashboard/{tenantId}/realtime?window=5m` to capture read-path baseline latency and error rate.

## Publish proof

After each benchmark run:

1. Save the `results-summary.json` artifact.
2. Update `docs/performance/latest-baseline.md` with requests/sec, P95/P99 latency, and error-rate values.
3. Link the corresponding pipeline run ID and deployment version.
