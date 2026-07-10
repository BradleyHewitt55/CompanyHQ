# MCP auditing

> **Document status:** Confirmed Target V1 security contract.

Audit session open/close/revoke, discovery, prompt/resource access, tool request, authorization decision, approval, command dispatch, output classification, cancellation, timeout, retry, and error. Include all attribution fields from [overview](overview.md), schema/tool version, duration, idempotency, correlation, and safe outcome.

Inputs/outputs are classified and hashed or stored as access-controlled artifacts when full retention is necessary. Secrets and hidden DTO fields are never audit payloads.
