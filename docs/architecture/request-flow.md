# Request flow

> **Document status:** Target V1 architecture contract.

1. Resolve the installation from server-owned domain configuration.
2. Establish correlation/trace IDs and apply request size, origin, and rate controls.
3. Authenticate the session, API client, webhook signature, job envelope, or MCP capability.
4. Construct immutable company and actor contexts.
5. Parse the operation with its versioned Zod contract.
6. Authorize the operation and compile record/field constraints.
7. Call the query or command application service.
8. For mutations, validate workflow, approval, idempotency, and expected record version inside the transaction.
9. Persist the mutation, immutable revision, audit record, and outbox events atomically.
10. Return a policy-projected DTO and safe error envelope.

Internal requests do not skip steps 4–9. UI visibility is a convenience derived from policy, never enforcement.
