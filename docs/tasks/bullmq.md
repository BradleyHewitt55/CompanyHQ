# BullMQ job processing

> **Document status:** Confirmed Target V1 technology and architecture contract.

Queues use `companyhq:{installationId}:{environment}:{queue}` prefixes. Job payloads are small, versioned Zod envelopes containing IDs and signed context, never secrets or full documents. PostgreSQL remains authoritative for task/run/schedule state.

Workers claim with BullMQ locks and also acquire a PostgreSQL run lease. They heartbeat both, reauthorize before each external effect, and commit outcomes with an attempt/version check. Retries use classified exponential backoff with jitter; authorization, validation, approval, ambiguous external-effect, and repeated deterministic failures do not auto-retry. Exhausted jobs enter a named dead-letter queue and operational incident state.

Idempotency keys are unique over installation, command/tool, target, semantic input version, and caller-supplied key. External adapters record request IDs and reconcile uncertain outcomes before retry. Cancellation sets durable intent, aborts the provider/tool when supported, and prevents later stale completion.

There is no product license cap on concurrent agents. Operator-configured worker concurrency, CPU/memory, provider rate limits, and health-based backpressure protect the installation.
