# MCP development guide

> **Document status:** Target V1 implementation guide.

To add a tool/resource/prompt: define the versioned Zod contract in `packages/contracts`; declare capability/risk/limits/audit; implement a thin handler using application-service ports; register it through config/extension SDK; add DTO projection; document it; and test schema, allow/deny matrices, installation/task isolation, revocation, idempotency, cancellation, timeout, redaction, audit, and transport parity.

CI rejects direct database/Redis/filesystem/shell imports, missing audit metadata, mutable tools without idempotency, unbounded output, duplicate names, undocumented contracts, and missing negative security tests.
