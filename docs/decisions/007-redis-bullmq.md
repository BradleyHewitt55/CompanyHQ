# ADR-007: Redis and BullMQ

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Agent and CMS jobs need scheduling, retries, locks, cancellation, and workers. Use Redis/BullMQ for delivery and PostgreSQL for authoritative task/run/schedule/outbox state.

## Alternatives considered
Database polling; RabbitMQ; Kafka; provider-native queues; cron files.

## Advantages, disadvantages, and risks
BullMQ fits TypeScript and operational needs. Redis loss/duplicate delivery are expected risks; idempotency, PostgreSQL leases, outbox, DLQ, and reconciliation make them safe.

## Consequences and migration
Queue payloads contain IDs/context only. No business state exists solely in Redis.
