# ADR-019: OpenTelemetry observability

> **Status:** Accepted. **Review:** 2027-01-15.

## Context and decision
Requests, jobs, agents, tools, and providers need correlated diagnostics. Use OpenTelemetry APIs with structured JSON logs and pluggable OTLP export.

## Alternatives considered
Vendor-specific instrumentation; logs only; custom tracing.

## Advantages, disadvantages, and risks
Portable correlation and standard tooling improve operations. Telemetry can leak/cost too much; redaction, sampling, cardinality rules, and retention are enforced centrally.

## Consequences and migration
Core contracts carry correlation/causation IDs. Audit remains separate from telemetry.
