# Cross-company isolation tests

> **Document status:** Target V1 release-gate contract.

Run two installations and attempt token/session/API-key replay; forged host/company headers; direct ID substitution; relationship expansion; search/export; cache and rate-key collisions; BullMQ job movement; webhook replay; MCP session/resource reuse; provider callback confusion; storage traversal; symlink/worktree escape; log/trace/audit leakage; backup restore mismatch; and extension/provider compromise.

Tests must prove deny at the source boundary, not merely UI hiding. They inspect response bodies, status/timing, database queries, queues, caches, objects, logs, metrics, traces, and audit. Any leakage is release-blocking.
