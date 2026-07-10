# Custom endpoints

> **Document status:** Target V1 architecture contract.

Custom endpoints register method, route, request/response Zod schemas, authentication class, authorization action, rate category, idempotency behavior, audit event, and application query/command handler. They cannot receive raw request-to-database access.

Routes under `/api/v{major}` are collision-checked against core and extension routes. Errors use the standard safe envelope. Webhooks are separate verified ingress adapters and enqueue durable commands after signature/replay checks.
