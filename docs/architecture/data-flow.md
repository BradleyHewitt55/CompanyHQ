# Data flow

> **Document status:** Target V1 architecture contract.

```mermaid
sequenceDiagram
  actor Caller
  participant Transport as Web/API/MCP/Worker
  participant App as Command service
  participant Policy as Authorization
  participant DB as PostgreSQL transaction
  participant Outbox
  participant Queue as BullMQ
  Caller->>Transport: typed request + credentials/context
  Transport->>App: command envelope
  App->>Policy: actor, action, resource, attributes
  Policy-->>App: deny or constrained grant
  App->>DB: validate version + mutate + revision + audit
  App->>Outbox: append events in same transaction
  DB-->>App: committed result
  App-->>Caller: policy-projected DTO
  Outbox->>Queue: idempotent publication
  Queue->>App: context-bound background command
```

Reads use query services that compile authorized filters before SQL and project registered DTOs before serialization. Writes never expose database records as responses. Relationship expansion repeats access checks and enforces depth/count limits.

Redis caches only reconstructible data. Cache invalidation keys include installation and configuration revision. Outbox publication uses a unique event ID; consumers record idempotency before external effects.
