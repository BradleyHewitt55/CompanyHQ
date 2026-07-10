# Observability

> **Document status:** Target V1 architecture contract.

OpenTelemetry traces connect HTTP/MCP, authentication, authorization, application commands, SQL, outbox, BullMQ, provider sessions, tools, source control, storage, and webhooks. Metrics cover latency/error/saturation, queue lag/age/retries/DLQ, worker/provider health, lease expiry, schedules, database/Redis/storage health, token/cost usage, approvals, conflicts, and audit failures.

High-cardinality IDs belong in spans/logs, not metric labels. Dashboards and alerts are per installation and environment. Run history links safe traces, audit, task evidence, and tool records for debugging without reconstructing secrets.
