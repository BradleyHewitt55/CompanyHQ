# MCP retries

> **Document status:** Target V1 operational contract.

Read calls may retry transient transport/unavailable errors within the deadline. Mutation retries require a caller idempotency key and server receipt. The server stores command outcome before returning and replays that outcome for duplicates.

Authorization, validation, conflict, approval, cancellation, and deterministic tool failures are not retried. For uncertain provider/source-control effects, reconcile using recorded external request IDs before deciding.
