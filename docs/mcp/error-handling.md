# MCP error handling

> **Document status:** Target V1 contract.

Errors map to stable codes: unauthenticated, forbidden/not-found-safe, invalid input, conflict, approval required, rate limited, deadline exceeded, cancelled, unavailable, incompatible version, and internal. Responses include safe message, correlation ID, retryability, and structured field issues where authorized.

Raw exceptions, SQL/provider output, stack traces, host paths, command lines, secrets, and resource-existence clues are never returned. Ambiguous external effects return reconciliation-required, not a retry hint.
